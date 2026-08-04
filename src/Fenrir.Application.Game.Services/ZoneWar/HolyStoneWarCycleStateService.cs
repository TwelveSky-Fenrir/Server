using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ZoneWar;

public sealed class HolyStoneWarCycleStateService(
    HolyStoneWarCycle cycle,
    IWorldEventSnapshotRepository repository,
    ILogger<HolyStoneWarCycleStateService> logger,
    TimeProvider? timeProvider = null)
{
    private const string EventKind = "holy-stone-war";
    private const string OccurrenceKey = "zone-038";
    private const int PayloadVersion = 1;
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private bool _captureFinalizationAuthorized;
    private DateTimeOffset _lastFlushAtUtc;
    private long _revision;

    public bool IsInitialized { get; private set; }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsInitialized)
                throw new InvalidOperationException("Holy Stone cycle state has already been initialized.");

            var loaded = await LoadAsync(ct).ConfigureAwait(false);
            if (loaded is null)
            {
                cycle.Restore(new HolyStoneWarCycleSnapshot(HolyStoneWarPhase.Cooldown, 0,
                    new MinuteCountdownSnapshot(0, 0), null));
                _revision = 0;
                IsInitialized = true;
                await PersistAsync(cycle.Snapshot(), ct).ConfigureAwait(false);
                return;
            }

            _revision = loaded.Value.Revision;
            cycle.Restore(loaded.Value.Snapshot);
            IsInitialized = true;

            if (cycle.HasPendingCaptureFinalization)
            {
                logger.LogWarning(
                    "Holy Stone capture finalization was recovered after a restart and is being closed without replaying effects");
                cycle.CloseRecoveredCaptureFinalization();
                await FlushDirtyAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask TickAsync(TimeSpan elapsed, CancellationToken ct)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            cycle.Tick(elapsed);

            if (cycle.HasPendingCaptureFinalization)
            {
                if (!_captureFinalizationAuthorized)
                    await FlushDirtyAsync(ct).ConfigureAwait(false);

                if (!_captureFinalizationAuthorized)
                    return;

                _captureFinalizationAuthorized = false;
                cycle.FinalizePersistedCapture();
                await FlushDirtyAsync(ct).ConfigureAwait(false);
                return;
            }

            if (_timeProvider.GetUtcNow() - _lastFlushAtUtc >= FlushInterval)
                await FlushDirtyAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask FlushAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            await FlushDirtyAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask FlushDirtyAsync(CancellationToken ct)
    {
        if (!cycle.TryGetDirtySnapshot(out var snapshot))
            return;

        if (await TryPersistAsync(snapshot, ct).ConfigureAwait(false))
            return;

        var loaded = await LoadAsync(ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "Holy Stone cycle snapshot disappeared after a revision conflict.");

        _revision = loaded.Revision;
        _captureFinalizationAuthorized = false;
        cycle.Restore(loaded.Snapshot);
        if (cycle.HasPendingCaptureFinalization)
            cycle.CloseRecoveredCaptureFinalization();
    }

    private async ValueTask<bool> TryPersistAsync(HolyStoneWarCycleSnapshot snapshot, CancellationToken ct)
    {
        var payload = Serialize(snapshot);
        var payloadHash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var applied = await repository.TryApplyAsync(EventKind, OccurrenceKey, _revision, snapshot.Phase.ToString(),
            payload, payloadHash, ct).ConfigureAwait(false);
        if (!applied)
            return false;

        _revision = checked(_revision + 1);
        _lastFlushAtUtc = _timeProvider.GetUtcNow();
        cycle.AcknowledgePersisted(snapshot);
        _captureFinalizationAuthorized = snapshot.Phase == HolyStoneWarPhase.CaptureFinalizationPending;
        return true;
    }

    private async ValueTask PersistAsync(HolyStoneWarCycleSnapshot snapshot, CancellationToken ct)
    {
        if (!await TryPersistAsync(snapshot, ct).ConfigureAwait(false))
            throw new InvalidOperationException("Holy Stone cycle snapshot creation conflicted unexpectedly.");
    }

    private async ValueTask<LoadedSnapshot?> LoadAsync(CancellationToken ct)
    {
        var rows = await repository.LoadAllAsync(ct).ConfigureAwait(false);
        WorldEventSnapshotRowDto? match = null;
        foreach (var row in rows)
        {
            if (!string.Equals(row.EventKind, EventKind, StringComparison.Ordinal) ||
                !string.Equals(row.OccurrenceKey, OccurrenceKey, StringComparison.Ordinal))
                continue;

            if (match is not null)
                throw new InvalidOperationException(
                    "Holy Stone cycle storage contains duplicate snapshot occurrences.");

            match = row;
        }

        if (match is null)
            return null;

        if (match.Revision <= 0 || match.CanonicalPayloadHash is not { Length: 32 })
            throw new InvalidOperationException("Holy Stone cycle snapshot has an invalid durable revision or hash.");

        var computedHash = SHA256.HashData(Encoding.UTF8.GetBytes(match.CanonicalPayload));
        if (!CryptographicOperations.FixedTimeEquals(computedHash, match.CanonicalPayloadHash))
            throw new InvalidOperationException(
                "Holy Stone cycle snapshot payload hash does not match its stored payload.");

        var snapshot = Deserialize(match.CanonicalPayload);
        if (!string.Equals(match.Phase, snapshot.Phase.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Holy Stone cycle snapshot phase disagrees with its canonical payload.");

        return new LoadedSnapshot(match.Revision, snapshot);
    }

    private static string Serialize(in HolyStoneWarCycleSnapshot snapshot)
    {
        if (!snapshot.HasExpectedShape)
            throw new ArgumentException("The Holy Stone cycle snapshot is invalid.", nameof(snapshot));

        return string.Create(CultureInfo.InvariantCulture,
            $"{PayloadVersion}|{(byte)snapshot.Phase}|{snapshot.CooldownRemainingTicks}|{snapshot.Countdown.AccumulatedTicks}|{snapshot.Countdown.MinutesElapsed}|{snapshot.PendingCandidateCharacterId ?? 0}");
    }

    private static HolyStoneWarCycleSnapshot Deserialize(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        var parts = payload.Split('|');
        if (parts.Length != 6 || !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var version) ||
            version != PayloadVersion || !byte.TryParse(parts[1], CultureInfo.InvariantCulture, out var phaseValue) ||
            !long.TryParse(parts[2], CultureInfo.InvariantCulture, out var cooldownTicks) ||
            !long.TryParse(parts[3], CultureInfo.InvariantCulture, out var accumulatedTicks) ||
            !int.TryParse(parts[4], CultureInfo.InvariantCulture, out var minutesElapsed) ||
            !int.TryParse(parts[5], CultureInfo.InvariantCulture, out var pendingCandidateId))
            throw new InvalidOperationException("Holy Stone cycle snapshot has an invalid canonical payload.");

        var snapshot = new HolyStoneWarCycleSnapshot((HolyStoneWarPhase)phaseValue, cooldownTicks,
            new MinuteCountdownSnapshot(accumulatedTicks, minutesElapsed),
            pendingCandidateId == 0 ? null : pendingCandidateId);
        if (!snapshot.HasExpectedShape || !string.Equals(payload, Serialize(snapshot), StringComparison.Ordinal))
            throw new InvalidOperationException("Holy Stone cycle snapshot is not canonical.");

        return snapshot;
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException(
                "Holy Stone cycle state is unavailable until its durable snapshot is loaded.");
    }

    private readonly record struct LoadedSnapshot(long Revision, HolyStoneWarCycleSnapshot Snapshot);
}
