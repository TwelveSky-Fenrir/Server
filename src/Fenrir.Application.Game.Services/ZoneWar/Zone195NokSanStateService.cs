using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Services.ZoneWar;

public enum Zone195NokSanPersistenceOutcome : byte
{
    NotInitialized = 0,

    NoChanges = 1,

    Applied = 2,

    ConflictReconciled = 3
}

public sealed class Zone195NokSanStateService(
    Zone195NokSanState state,
    IZone195NokSanStateRepository repository)
{
    private readonly SemaphoreSlim _persistenceGate = new(1, 1);

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _persistenceGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            state.Initialize(await LoadSnapshotAsync(ct).ConfigureAwait(false));
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

        public async ValueTask<Zone195NokSanPersistenceOutcome> FlushIfDirtyAsync(CancellationToken ct)
    {
        await _persistenceGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!state.IsInitialized)
                return Zone195NokSanPersistenceOutcome.NotInitialized;

            if (!state.TryGetDirtySnapshot(out var snapshot))
                return Zone195NokSanPersistenceOutcome.NoChanges;

            var applied = await repository.TrySaveAsync(ToRow(snapshot), ToRows(snapshot), ct).ConfigureAwait(false);
            if (applied)
            {
                state.AcknowledgePersisted(snapshot);
                return Zone195NokSanPersistenceOutcome.Applied;
            }

            state.Reconcile(await LoadSnapshotAsync(ct).ConfigureAwait(false), snapshot);
            return Zone195NokSanPersistenceOutcome.ConflictReconciled;
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        await _persistenceGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!state.IsInitialized)
                return;

            var durable = await LoadSnapshotAsync(ct).ConfigureAwait(false);
            state.Reconcile(durable);
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

    private async ValueTask<Zone195NokSanDurableSnapshot> LoadSnapshotAsync(CancellationToken ct)
    {
        var (storedState, storedCaptures) = await repository.LoadAsync(ct).ConfigureAwait(false);
        if (storedState is null)
        {
            if (!storedCaptures.IsEmpty)
                throw new InvalidOperationException(
                    "Nok-San capture rows exist without their singleton owner/count row.");

            return Zone195NokSanState.CreateEmptySnapshot();
        }

        if (storedCaptures.Length != 3)
            throw new InvalidOperationException(
                "The Nok-San durable row must be accompanied by exactly three active-site capture rows.");

        return new Zone195NokSanDurableSnapshot(
            storedState.Revision,
            0,
            new Zone195NokSanStateSnapshot(
                [storedState.StonesHeld0, storedState.StonesHeld1, storedState.StonesHeld2, storedState.StonesHeld3],
                [storedState.OwnerSlot0, 0, storedState.OwnerSlot2, storedState.OwnerSlot3, 0, 0, 0, 0, 0]),
            [
                ToCaptureSnapshot(GetCapture(storedCaptures, 99)),
                ToCaptureSnapshot(GetCapture(storedCaptures, 100)),
                ToCaptureSnapshot(GetCapture(storedCaptures, 196))
            ]);
    }

    private static Zone195NokSanStateRowDto ToRow(in Zone195NokSanDurableSnapshot snapshot)
    {
        return new Zone195NokSanStateRowDto(
            snapshot.Revision,
            checked((byte)snapshot.State.Owners[Zone195NokSanState.Server196StoneSlotIndex]),
            checked((byte)snapshot.State.Owners[Zone195NokSanState.Server99StoneSlotIndex]),
            checked((byte)snapshot.State.Owners[Zone195NokSanState.Server100StoneSlotIndex]),
            checked((byte)snapshot.State.StonesHeld[0]),
            checked((byte)snapshot.State.StonesHeld[1]),
            checked((byte)snapshot.State.StonesHeld[2]),
            checked((byte)snapshot.State.StonesHeld[3]),
            DateTime.UnixEpoch);
    }

    private static ImmutableArray<Zone195NokSanCaptureRowDto> ToRows(in Zone195NokSanDurableSnapshot snapshot)
    {
        return [
            ToRow(GetCapture(snapshot.Captures, 99)),
            ToRow(GetCapture(snapshot.Captures, 100)),
            ToRow(GetCapture(snapshot.Captures, 196))
        ];
    }

    private static Zone195NokSanCaptureRowDto ToRow(in Zone195NokSanCaptureSnapshot snapshot)
    {
        return new Zone195NokSanCaptureRowDto(snapshot.MapId, (byte)snapshot.Phase, snapshot.CapturerCharacterId,
            snapshot.CapturerTribe, snapshot.CapturerName, snapshot.RemainingTime, snapshot.PhaseAccumulatorTicks);
    }

    private static Zone195NokSanCaptureSnapshot ToCaptureSnapshot(Zone195NokSanCaptureRowDto row)
    {
        return new Zone195NokSanCaptureSnapshot(row.MapId, (Zone195CapturePhase)row.Phase, row.CapturerCharacterId,
            row.CapturerTribe, row.CapturerName, row.RemainingTime, row.PhaseAccumulatorTicks);
    }

    private static Zone195NokSanCaptureRowDto GetCapture(
        ImmutableArray<Zone195NokSanCaptureRowDto> captures, short mapId)
    {
        Zone195NokSanCaptureRowDto? match = null;
        foreach (var capture in captures)
        {
            if (capture.MapId != mapId)
                continue;

            if (match is not null)
                throw new InvalidOperationException($"Nok-San map {mapId} appears more than once in durable storage.");

            match = capture;
        }

        return match ?? throw new InvalidOperationException($"Nok-San map {mapId} is absent from durable storage.");
    }

    private static Zone195NokSanCaptureSnapshot GetCapture(
        ImmutableArray<Zone195NokSanCaptureSnapshot> captures, short mapId)
    {
        Zone195NokSanCaptureSnapshot? match = null;
        foreach (var capture in captures)
        {
            if (capture.MapId != mapId)
                continue;

            if (match is not null)
                throw new InvalidOperationException($"Nok-San map {mapId} appears more than once in the local snapshot.");

            match = capture;
        }

        return match ?? throw new InvalidOperationException($"Nok-San map {mapId} is absent from the local snapshot.");
    }
}
