using System.Collections.Concurrent;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     One connection's recorded TEMP_REGISTER_SEND (op11/ZoneHandshake) admission. <see cref="Tribe" /> is
///     always the upstream-resolved (authoritative) tribe the identity-resolution step returned, never the
///     client-declared tribe that was only used for the threshold comparison at admission time -- see
///     <see cref="TribeQuotaRegistry" />'s own remarks for why the two can disagree.
/// </summary>
public sealed record TribeQuotaEntry(
    ZoneClientSession Session,
    int Tribe,
    int AccountId,
    int CharacterId,
    DateTimeOffset RegisteredAtUtc);

/// <summary>
///     Process-wide directory of every connection currently holding a TEMP_REGISTER_SEND (op11/ZoneHandshake)
///     admission, keyed by session. There is no independently maintained per-tribe counter: "how many
///     connections currently hold each tribe" is always recomputed by scanning every recorded entry at the
///     moment a new attempt is evaluated (<see cref="CountForTribe" />), never cached or updated incrementally
///     -- matching the translated behavior contract's own Side effects §4.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:728-741 (upstream call, storage of the upstream-resolved
///     tribe versus the client-declared tribe used only for the threshold comparison, registration timestamp)
///     ; Server/ts25zone/H07_MyGame.h:778-779,785,794 (per-connection state this type mirrors: temp-registered
///     flag, recorded tribe, connect-state flag, registration timestamp) ; Server/ts25zone/S07_MyGame01.cpp:1963-1990
///     (periodic idle-timeout cleanup, see <see cref="TempRegistrationIdleSweep" />) ;
///     Server/ts25zone/S03_MyUser.cpp:340-411,413-421 (disconnect handling clears the temp-registered flag --
///     Fenrir's equivalent is <see cref="Release" />, called unconditionally from GameConnectionHost's own
///     connection-teardown path regardless of how far the connection got).
/// </remarks>
public sealed class TribeQuotaRegistry
{
    private readonly ConcurrentDictionary<long, TribeQuotaEntry> _entries = new();

    /// <summary>Every connection currently recorded as temp-registered, across every tribe -- diagnostic/test use only.</summary>
    public int Count => _entries.Count;

    /// <summary>
    ///     Full scan, not a cached counter (see this type's own remarks) -- fine for a once-per-connection
    ///     admission check, never a per-tick hot path.
    /// </summary>
    public int CountForTribe(int tribe)
    {
        var count = 0;
        foreach (var entry in _entries.Values)
            if (entry.Tribe == tribe)
                count++;

        return count;
    }

    /// <summary>
    ///     Records (or re-records, on a same-session replay) this connection's admission. <paramref name="tribe" />
    ///     must already be the upstream-resolved tribe, never the client-declared one.
    /// </summary>
    public void Record(ZoneClientSession session, int tribe, int accountId, int characterId,
        DateTimeOffset registeredAtUtc)
    {
        _entries[session.SessionId] = new TribeQuotaEntry(session, tribe, accountId, characterId, registeredAtUtc);
    }

    /// <summary>
    ///     Frees this session's counted slot. Idempotent (a session never recorded, or already released, is a
    ///     no-op) -- safe to call unconditionally from every connection-teardown path, matching legacy's own
    ///     unconditional temp-registered-flag clear on disconnect.
    /// </summary>
    public bool Release(long sessionId)
    {
        return _entries.TryRemove(sessionId, out _);
    }

    /// <summary>
    ///     Every recorded connection that has not yet reached <see cref="ZoneSessionState.InWorld" /> and was
    ///     registered at least <paramref name="idleTimeout" /> ago as of <paramref name="nowUtc" /> -- the
    ///     abandoned-registration set <see cref="TempRegistrationIdleSweep" /> forcibly disconnects.
    /// </summary>
    public IReadOnlyList<TribeQuotaEntry> SnapshotIdle(TimeSpan idleTimeout, DateTimeOffset nowUtc)
    {
        List<TribeQuotaEntry>? idle = null;

        foreach (var entry in _entries.Values)
            if (entry.Session.State != ZoneSessionState.InWorld && nowUtc - entry.RegisteredAtUtc >= idleTimeout)
                (idle ??= []).Add(entry);

        return idle ?? (IReadOnlyList<TribeQuotaEntry>)[];
    }
}
