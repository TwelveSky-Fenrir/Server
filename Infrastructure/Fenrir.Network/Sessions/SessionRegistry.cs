using System.Collections.Concurrent;

namespace Fenrir.Network.Sessions;

/// <summary>
///     Tracks live sessions and enforces "one connection per account" (architecture reference §8.1). Lifecycle
///     (Abort/CompleteAsync) stays with the caller — this type is pure bookkeeping.
/// </summary>
public sealed class SessionRegistry
{
    private readonly ConcurrentDictionary<long, long> _accountToSession = new();

    // Only needed to make the read-check-write around eviction in AssociateAccount atomic; everything else here is lock-free.
    private readonly Lock _associationLock = new();
    private readonly ConcurrentDictionary<long, ClientSession> _sessions = new();

    // Reverse of _accountToSession, kept so Unregister can drop the account-side entry from just a SessionId.
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

    public void Unregister(long sessionId)
    {
        _sessions.TryRemove(sessionId, out _);

        // Same lock as AssociateAccount: without it, Unregister racing the first AssociateAccount call for this
        // very session could leave _accountToSession pointing at a sessionId already dropped from _sessions —
        // a ghost association that defeats the one-connection-per-account guarantee until the next real login.
        lock (_associationLock)
        {
            // Conditional remove: only drop the account mapping if it still points at this session, so we never
            // erase a newer session's association raced in concurrently by AssociateAccount.
            if (_sessionToAccount.TryRemove(sessionId, out var accountId))
                _accountToSession.TryRemove(new KeyValuePair<long, long>(accountId, sessionId));
        }
    }

    /// <summary>
    ///     Binds <paramref name="sessionId" /> to <paramref name="accountId" />. If another session already held that
    ///     account, it is aborted with <see cref="DisconnectReason.Evicted" /> and returned — the new session is never
    ///     touched.
    /// </summary>
    public ClientSession? AssociateAccount(long sessionId, long accountId)
    {
        ClientSession? evicted = null;

        lock (_associationLock)
        {
            // Defensive: a session re-associating to a different account must not leave its old reverse pointer dangling.
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

        // Called outside the lock: Abort only cancels pending pipe operations, never re-enters the registry.
        evicted?.Abort(DisconnectReason.Evicted);
        return evicted;
    }
}
