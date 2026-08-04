using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Services.ZoneWar;

public enum ValleyWarCampaignPersistenceOutcome : byte
{
    NotInitialized = 0,
    NoChanges = 1,
    Applied = 2,
    ConflictReconciled = 3
}

public sealed class ValleyWarCampaignStateService(
    ValleyWarKillRegistry registry,
    IWorldEventSnapshotRepository snapshots)
{
    public const string EventKind = "valley-war";
    public const string OccurrenceKey = "zone200";

    private readonly SemaphoreSlim _persistenceGate = new(1, 1);

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _persistenceGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            registry.Initialize(await LoadAsync(ct).ConfigureAwait(false));
        }
        catch
        {
            registry.MarkUnavailable();
            throw;
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

    public async ValueTask<ValleyWarCampaignPersistenceOutcome> FlushIfDirtyAsync(CancellationToken ct)
    {
        await _persistenceGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!registry.IsAvailable)
                return ValleyWarCampaignPersistenceOutcome.NotInitialized;

            if (!registry.TryGetDirtySnapshot(out var snapshot))
                return ValleyWarCampaignPersistenceOutcome.NoChanges;

            var canonicalPayload = JsonSerializer.Serialize(snapshot.Schedule,
                ValleyWarScheduleJsonContext.Default.ValleyWarScheduleState);
            var canonicalPayloadHash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload));
            var applied = await snapshots.TryApplyAsync(EventKind, OccurrenceKey, snapshot.Revision,
                    snapshot.Schedule.Phase.ToString(), canonicalPayload, canonicalPayloadHash, ct)
                .ConfigureAwait(false);
            if (applied)
            {
                registry.AcknowledgePersisted(snapshot);
                return ValleyWarCampaignPersistenceOutcome.Applied;
            }

            registry.Reconcile(await LoadAsync(ct).ConfigureAwait(false));
            return ValleyWarCampaignPersistenceOutcome.ConflictReconciled;
        }
        catch
        {
            registry.MarkUnavailable();
            throw;
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
            registry.Reconcile(await LoadAsync(ct).ConfigureAwait(false));
        }
        catch
        {
            registry.MarkUnavailable();
            throw;
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

    private async ValueTask<ValleyWarCampaignSnapshot> LoadAsync(CancellationToken ct)
    {
        var rows = await snapshots.LoadAllAsync(ct).ConfigureAwait(false);
        WorldEventSnapshotRowDto? matched = null;
        foreach (var row in rows)
        {
            if (!string.Equals(row.EventKind, EventKind, StringComparison.Ordinal) ||
                !string.Equals(row.OccurrenceKey, OccurrenceKey, StringComparison.Ordinal))
                continue;

            if (matched is not null)
                throw new InvalidOperationException("The Zone 200 campaign has more than one durable snapshot.");

            matched = row;
        }

        if (matched is null)
            return new ValleyWarCampaignSnapshot(0, 0, new ValleyWarSchedule().Snapshot());

        if (matched.Revision < 1 || matched.CanonicalPayloadHash is not { Length: SHA256.HashSizeInBytes })
            throw new InvalidOperationException("The Zone 200 campaign durable snapshot is malformed.");

        var canonicalPayloadHash = SHA256.HashData(Encoding.UTF8.GetBytes(matched.CanonicalPayload));
        if (!CryptographicOperations.FixedTimeEquals(canonicalPayloadHash, matched.CanonicalPayloadHash))
            throw new InvalidOperationException("The Zone 200 campaign durable snapshot hash is invalid.");

        var schedule = JsonSerializer.Deserialize(matched.CanonicalPayload,
                           ValleyWarScheduleJsonContext.Default.ValleyWarScheduleState) ??
                       throw new InvalidOperationException("The Zone 200 campaign durable snapshot has no schedule.");
        if (!string.Equals(matched.Phase, schedule.Phase.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException("The Zone 200 campaign durable phase disagrees with its schedule.");

        return new ValleyWarCampaignSnapshot(matched.Revision, 0, schedule);
    }
}
