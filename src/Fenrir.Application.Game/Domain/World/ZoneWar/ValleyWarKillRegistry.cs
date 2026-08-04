using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class ValleyWarKillRegistry
{
    private readonly ConcurrentDictionary<ValleyWarCampaignKey, ValleyWarSchedule> _schedules = new();
    private readonly Lock _stateLock = new();
    private bool _available;
    private bool _initialized;
    private long _lastPersistedGeneration;
    private long _mutationGeneration;
    private long _revision;

    public bool IsAvailable
    {
        get
        {
            lock (_stateLock)
            {
                return _initialized && _available;
            }
        }
    }

    public int GetWorldInfoState()
    {
        lock (_stateLock)
        {
            if (!_initialized || !_available ||
                !_schedules.TryGetValue(ValleyWarCampaignKey.Zone200, out var schedule))
                return (int)ValleyWarPhase.Idle;

            return (int)schedule.Phase;
        }
    }

    private ValleyWarSchedule GetOrCreate(short mapId)
    {
        if (!ValleyWarMapCatalog.TryGetCampaignKey(mapId, out var campaignKey))
            throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Map is not part of a Valley War campaign.");

        return _schedules.GetOrAdd(campaignKey, static _ => new ValleyWarSchedule());
    }

    public bool RegisterMonsterKill(short mapId, byte tribeId)
    {
        if (!ValleyWarMapCatalog.TryGetCampaignKey(mapId, out var campaignKey) || !IsAvailable)
            return false;

        var accepted = _schedules.GetOrAdd(campaignKey, static _ => new ValleyWarSchedule())
            .RegisterMonsterKill(tribeId);
        if (accepted)
            MarkChanged();

        return accepted;
    }

    public bool TryTick(short mapId, ValleyWarEnvironmentSnapshot environment, out ValleyWarSchedule schedule,
        out ValleyWarTickResult result)
    {
        if (!ValleyWarMapCatalog.TryGetCampaignKey(mapId, out var campaignKey) || !IsAvailable)
        {
            schedule = null!;
            result = default;
            return false;
        }

        schedule = _schedules.GetOrAdd(campaignKey, static _ => new ValleyWarSchedule());
        result = schedule.Tick(environment);
        MarkChanged();
        return true;
    }

    public void Initialize(ValleyWarCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Revision < 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot));

        lock (_stateLock)
        {
            if (_initialized)
                throw new InvalidOperationException("The valley-war campaign state has already been initialized.");

            GetOrCreate(ValleyWarMapCatalog.CoordinatorMapId).Restore(snapshot.Schedule);
            _revision = snapshot.Revision;
            _lastPersistedGeneration = 0;
            _mutationGeneration = 0;
            _initialized = true;
            _available = true;
        }
    }

    public bool TryGetDirtySnapshot(out ValleyWarCampaignSnapshot snapshot)
    {
        lock (_stateLock)
        {
            if (!_initialized || !_available || _mutationGeneration == _lastPersistedGeneration)
            {
                snapshot = default!;
                return false;
            }

            snapshot = new ValleyWarCampaignSnapshot(_revision, _mutationGeneration,
                GetOrCreate(ValleyWarMapCatalog.CoordinatorMapId).Snapshot());
            return true;
        }
    }

    public void AcknowledgePersisted(ValleyWarCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_stateLock)
        {
            if (!_initialized || !_available || snapshot.Revision != _revision ||
                snapshot.Generation > _mutationGeneration)
                throw new InvalidOperationException("The persisted valley-war campaign snapshot is no longer current.");

            _revision = checked(snapshot.Revision + 1);
            _lastPersistedGeneration = snapshot.Generation;
        }
    }

    public void Reconcile(ValleyWarCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Revision < 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot));

        lock (_stateLock)
        {
            if (_initialized && snapshot.Revision <= _revision)
                return;

            GetOrCreate(ValleyWarMapCatalog.CoordinatorMapId).Restore(snapshot.Schedule);
            _revision = snapshot.Revision;
            _mutationGeneration = 0;
            _lastPersistedGeneration = 0;
            _initialized = true;
            _available = true;
        }
    }

    public void MarkUnavailable()
    {
        lock (_stateLock)
        {
            _available = false;
        }
    }

    private void MarkChanged()
    {
        lock (_stateLock)
        {
            if (_initialized && _available)
                _mutationGeneration = checked(_mutationGeneration + 1);
        }
    }
}

public sealed record ValleyWarCampaignSnapshot(long Revision, long Generation, ValleyWarScheduleState Schedule);
