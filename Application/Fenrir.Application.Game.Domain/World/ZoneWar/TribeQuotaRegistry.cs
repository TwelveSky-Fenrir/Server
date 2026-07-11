using System.Collections.Concurrent;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed record TribeQuotaEntry(
    ZoneClientSession Session,
    int Tribe,
    int AccountId,
    int CharacterId,
    DateTimeOffset RegisteredAtUtc);

public sealed class TribeQuotaRegistry
{
    private readonly ConcurrentDictionary<long, TribeQuotaEntry> _entries = new();

        public int Count => _entries.Count;

        public int CountForTribe(int tribe)
    {
        var count = 0;
        foreach (var entry in _entries.Values)
            if (entry.Tribe == tribe)
                count++;

        return count;
    }

        public void Record(ZoneClientSession session, int tribe, int accountId, int characterId,
        DateTimeOffset registeredAtUtc)
    {
        _entries[session.SessionId] = new TribeQuotaEntry(session, tribe, accountId, characterId, registeredAtUtc);
    }

        public bool Release(long sessionId)
    {
        return _entries.TryRemove(sessionId, out _);
    }

        public IReadOnlyList<TribeQuotaEntry> SnapshotIdle(TimeSpan idleTimeout, DateTimeOffset nowUtc)
    {
        List<TribeQuotaEntry>? idle = null;

        foreach (var entry in _entries.Values)
            if (entry.Session.State != ZoneSessionState.InWorld && nowUtc - entry.RegisteredAtUtc >= idleTimeout)
                (idle ??= []).Add(entry);

        return idle ?? (IReadOnlyList<TribeQuotaEntry>)[];
    }
}
