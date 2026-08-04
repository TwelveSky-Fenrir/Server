using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed record TribeQuotaEntry(
    short MapId,
    IZoneSession Session,
    int Tribe,
    int AccountId,
    int CharacterId,
    DateTimeOffset RegisteredAtUtc);

public sealed class TribeQuotaRegistry
{
    private readonly Dictionary<TribeQuotaKey, TribeQuotaEntry> _entries = new();
    private readonly Lock _lock = new();

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    public int CountForTribe(short mapId, int tribe)
    {
        lock (_lock)
        {
            return CountForTribeCore(mapId, tribe);
        }
    }

    public bool TryReserve(IZoneSession session, int tribe, int accountId, DateTimeOffset registeredAtUtc,
        TribeQuotaGroup quotaGroup, int capacity, out int population)
    {
        lock (_lock)
        {
            var key = new TribeQuotaKey(session.ListenerMapId, session.SessionId);
            population = CountForTribeCore(key.MapId, tribe);
            if (TribeQuotaGate.Evaluate(quotaGroup, capacity, population) == TribeQuotaOutcome.QuotaFull)
                return false;

            _entries[key] = new TribeQuotaEntry(key.MapId, session, tribe, accountId, 0, registeredAtUtc);
            return true;
        }
    }

    public void Record(IZoneSession session, int tribe, int accountId, int characterId,
        DateTimeOffset registeredAtUtc)
    {
        lock (_lock)
        {
            var key = new TribeQuotaKey(session.ListenerMapId, session.SessionId);
            _entries[key] = new TribeQuotaEntry(key.MapId, session, tribe, accountId, characterId, registeredAtUtc);
        }
    }

    public bool Release(IZoneSession session)
    {
        lock (_lock)
        {
            return _entries.Remove(new TribeQuotaKey(session.ListenerMapId, session.SessionId), out _);
        }
    }

    public IReadOnlyList<TribeQuotaEntry> SnapshotIdle(TimeSpan idleTimeout, DateTimeOffset nowUtc)
    {
        lock (_lock)
        {
            List<TribeQuotaEntry>? idle = null;

            foreach (var entry in _entries.Values)
                if (entry.Session.State != ZoneSessionState.InWorld && nowUtc - entry.RegisteredAtUtc >= idleTimeout)
                    (idle ??= []).Add(entry);

            return idle ?? (IReadOnlyList<TribeQuotaEntry>)[];
        }
    }

    private int CountForTribeCore(short mapId, int tribe)
    {
        var count = 0;
        foreach (var entry in _entries.Values)
            if (entry.MapId == mapId && entry.Tribe == tribe)
                count++;

        return count;
    }

    private readonly record struct TribeQuotaKey(short MapId, long SessionId);
}
