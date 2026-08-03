using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed record TribeQuotaEntry(
    IZoneSession Session,
    int Tribe,
    int AccountId,
    int CharacterId,
    DateTimeOffset RegisteredAtUtc);

public sealed class TribeQuotaRegistry
{
    private readonly Dictionary<long, TribeQuotaEntry> _entries = new();
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

    public int CountForTribe(int tribe)
    {
        lock (_lock)
        {
            return CountForTribeCore(tribe);
        }
    }

    /// <summary>
    ///     Checks <paramref name="tribe" />'s population against <paramref name="quotaGroup" />/
    ///     <paramref name="capacity" /> and reserves this session's slot in the same locked step, closing the
    ///     TOCTOU a separate read-then-await-then-record sequence would otherwise leave open. Finalize a
    ///     successful reservation with <see cref="Record" /> (its <see cref="TribeQuotaEntry.CharacterId" /> is a
    ///     0 placeholder until then) or undo it with <see cref="Release" /> if the caller rejects afterward.
    /// </summary>
    public bool TryReserve(IZoneSession session, int tribe, int accountId, DateTimeOffset registeredAtUtc,
        TribeQuotaGroup quotaGroup, int capacity, out int population)
    {
        lock (_lock)
        {
            population = CountForTribeCore(tribe);
            if (TribeQuotaGate.Evaluate(quotaGroup, capacity, population) == TribeQuotaOutcome.QuotaFull)
                return false;

            _entries[session.SessionId] = new TribeQuotaEntry(session, tribe, accountId, 0, registeredAtUtc);
            return true;
        }
    }

    public void Record(IZoneSession session, int tribe, int accountId, int characterId,
        DateTimeOffset registeredAtUtc)
    {
        lock (_lock)
        {
            _entries[session.SessionId] =
                new TribeQuotaEntry(session, tribe, accountId, characterId, registeredAtUtc);
        }
    }

    public bool Release(long sessionId)
    {
        lock (_lock)
        {
            return _entries.Remove(sessionId, out _);
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

    private int CountForTribeCore(int tribe)
    {
        var count = 0;
        foreach (var entry in _entries.Values)
            if (entry.Tribe == tribe)
                count++;

        return count;
    }
}
