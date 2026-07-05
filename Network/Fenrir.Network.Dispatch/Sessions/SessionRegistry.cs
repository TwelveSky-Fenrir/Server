using System.Collections.Concurrent;

namespace Fenrir.Network.Sessions;

// Enforces one connection per account lifecycle (Abort/CompleteAsync) stays with the caller.
public sealed class SessionRegistry
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

        // Outside the lock: Abort only cancels pending pipe operations, never re-enters the registry.
        evicted?.Abort(DisconnectReason.Evicted);
        return evicted;
    }
}
