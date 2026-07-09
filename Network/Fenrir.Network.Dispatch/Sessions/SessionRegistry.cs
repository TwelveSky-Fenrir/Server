using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Sessions;

// Enforces one connection per account lifecycle (Abort/CompleteAsync) stays with the caller.
public sealed class SessionRegistry(ILogger<SessionRegistry>? logger = null)
{
    private readonly ConcurrentDictionary<long, long> _accountToSession = new();

    // Guards only the read-check-write eviction race in AssociateAccount; everything else here is lock-free.
    private readonly Lock _associationLock = new();
    private readonly ConcurrentDictionary<long, ClientSession> _sessions = new();

    // Reverse of _accountToSession, so Unregister can drop the account-side entry from just a SessionId.
    private readonly ConcurrentDictionary<long, long> _sessionToAccount = new();

    public int Count => _sessions.Count;

    public void Register(ClientSession session)
    {
        _sessions[session.SessionId] = session;
    }

    public bool TryGet(long sessionId, out ClientSession? session)
    {
        return _sessions.TryGetValue(sessionId, out session);
    }

    // Cross-process duplicate-login kick/refusal: lets a caller resolve "is this account's session held by
    // this process, and which one" without walking every registered session.
    public bool TryGetByAccount(long accountId, out ClientSession? session)
    {
        if (_accountToSession.TryGetValue(accountId, out var sessionId))
            return _sessions.TryGetValue(sessionId, out session);

        session = null;
        return false;
    }

    /// <summary>
    ///     Best-effort snapshot of every AccountId this process currently holds a local session for --
    ///     used by the periodic cross-process liveness/kick polls, never by the synchronous login-time check.
    /// </summary>
    public ImmutableArray<long> SnapshotAssociatedAccountIds()
    {
        return [.._accountToSession.Keys];
    }

    /// <summary>
    ///     Every currently-registered session whose captured <see cref="ClientSession.RemoteEndPoint" /> address
    ///     matches <paramref name="ipAddress" /> by exact string equality -- mirrors legacy's own
    ///     <c>IsMatchString</c>/<c>strcmp</c> matching (no CIDR/prefix/wildcard matching exists in the legacy
    ///     source this mirrors either). Used by <see cref="FloodProtection.IpFloodGuard" /> to kick every session
    ///     sharing an IP once that IP trips a flood threshold -- a rare, defensive event, not a hot path, so a
    ///     plain scan is fine here.
    /// </summary>
    public ImmutableArray<ClientSession> SnapshotByRemoteAddress(string ipAddress)
    {
        var builder = ImmutableArray.CreateBuilder<ClientSession>();

        foreach (var session in _sessions.Values)
            if (session.RemoteEndPoint?.Address.ToString() == ipAddress)
                builder.Add(session);

        return builder.ToImmutable();
    }

    /// <summary>
    ///     Every currently-registered session whose <see cref="ClientSession.LastActivityUtc" /> is STRICTLY more
    ///     than <paramref name="idleTimeout" /> old as of <paramref name="nowUtc" /> -- the abandoned-connection
    ///     set a per-server idle sweep (e.g. GameServer's <c>SessionLivenessSweep</c> or Login's own
    ///     <c>LoginSessionLivenessSweep</c>) forcibly disconnects. A plain scan, same "rare, defensive, not a hot
    ///     path" posture as <see cref="SnapshotByRemoteAddress" /> -- called at most once per sweep interval
    ///     (seconds, not per-tick), never per-frame.
    ///     Deliberately a strict `&gt;`, not `&gt;=`: a connection whose silence exactly equals the threshold at
    ///     the instant of a given sweep pass is not yet disconnected -- it is caught on a later pass once the
    ///     threshold is strictly exceeded, matching legacy's own comparison.
    /// </summary>
    public ImmutableArray<ClientSession> SnapshotIdle(TimeSpan idleTimeout, DateTimeOffset nowUtc)
    {
        var builder = ImmutableArray.CreateBuilder<ClientSession>();

        foreach (var session in _sessions.Values)
            if (nowUtc - session.LastActivityUtc > idleTimeout)
                builder.Add(session);

        return builder.ToImmutable();
    }

    public void Unregister(long sessionId)
    {
        _sessions.TryRemove(sessionId, out _);

        // Same lock as AssociateAccount, else a race can leave _accountToSession pointing at an already-dropped session.
        lock (_associationLock)
        {
            // Only drop the account mapping if it still points at this session, to not erase a newer race-in association.
            if (_sessionToAccount.TryRemove(sessionId, out var accountId))
                _accountToSession.TryRemove(new KeyValuePair<long, long>(accountId, sessionId));
        }
    }

    // Binds sessionId to accountId; if another session already held that account, it's evicted (DisconnectReason.Evicted) and returned.
    public ClientSession? AssociateAccount(long sessionId, long accountId)
    {
        ClientSession? evicted = null;

        lock (_associationLock)
        {
            // A session re-associating to a different account must not leave its old reverse pointer dangling.
            if (_sessionToAccount.TryGetValue(sessionId, out var previousAccountId) && previousAccountId != accountId)
                _accountToSession.TryRemove(new KeyValuePair<long, long>(previousAccountId, sessionId));

            if (_accountToSession.TryGetValue(accountId, out var previousSessionId) && previousSessionId != sessionId)
            {
                _sessions.TryGetValue(previousSessionId, out evicted);
                _sessionToAccount.TryRemove(new KeyValuePair<long, long>(previousSessionId, accountId));
            }

            _accountToSession[accountId] = sessionId;
            _sessionToAccount[sessionId] = accountId;
        }

        // Blind spot this closes: the evicted session's OWN SessionLoop eventually logs its own
        // "connection loop ended, disconnect reason Evicted" line (Information) once Abort() below unblocks
        // its read loop, but that line has no idea which account or which NEW session caused it -- and
        // neither LoginService.LoginAsync nor ZoneHandshakeHandler (this method's only two production callers)
        // logs anything about the session it just discarded via this method's return value. Logged here,
        // centrally, the same "structural, not incidental" reasoning ClientSession.LogSessionStateChanged
        // already applies -- an operator can now correlate "session 42 disconnected: Evicted" with exactly
        // which account and which new session displaced it, without either caller needing to remember to log
        // it themselves. Warning, not Information: this is a forced disconnect of another live connection, the
        // same severity SessionLoop already uses for its own abort-triggering violations, not a routine
        // lifecycle event.
        if (evicted is not null)
            logger?.LogWarning(
                "Account {AccountId}: evicting session {EvictedSessionId} in favor of session {SessionId}",
                accountId, evicted.SessionId, sessionId);

        // Outside the lock: Abort only cancels pending pipe operations, never re-enters the registry.
        evicted?.Abort(DisconnectReason.Evicted);
        return evicted;
    }
}
