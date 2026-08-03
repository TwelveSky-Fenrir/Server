using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using Fenrir.Network.Dispatch.FloodProtection;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Sessions;

public sealed class SessionRegistry : IFloodKickSink
{
    private static readonly Meter Meter = new("Fenrir.Network.Sessions");

    private readonly ConcurrentDictionary<long, long> _accountToSession = new();

    private readonly Lock _associationLock = new();

    private readonly ObservableGauge<int> _sessionCountGauge;
    private readonly ConcurrentDictionary<long, ClientSession> _sessions = new();

    private readonly ConcurrentDictionary<long, long> _sessionToAccount = new();

    private readonly ILogger<SessionRegistry>? logger;

    public SessionRegistry(ILogger<SessionRegistry>? logger = null)
    {
        this.logger = logger;
        _sessionCountGauge = Meter.CreateObservableGauge(
            "fenrir.sessions.active", () => _sessions.Count, null,
            "Number of client sessions currently registered on this server");
    }

    public int Count => _sessions.Count;

    public int KickByRemoteAddress(string ipAddress)
    {
        var kicked = SnapshotByRemoteAddress(ipAddress);

        foreach (var session in kicked)
            session.Abort(DisconnectReason.IpBlocked);

        return kicked.Length;
    }

    public void Register(ClientSession session)
    {
        _sessions[session.SessionId] = session;
    }

    public bool TryGet(long sessionId, out ClientSession? session)
    {
        return _sessions.TryGetValue(sessionId, out session);
    }

    public bool TryGetByAccount(long accountId, out ClientSession? session)
    {
        if (_accountToSession.TryGetValue(accountId, out var sessionId))
            return _sessions.TryGetValue(sessionId, out session);

        session = null;
        return false;
    }

    public ImmutableArray<long> SnapshotAssociatedAccountIds()
    {
        return [.._accountToSession.Keys];
    }

    public ImmutableArray<ClientSession> SnapshotByRemoteAddress(string ipAddress)
    {
        var builder = ImmutableArray.CreateBuilder<ClientSession>();

        foreach (var session in _sessions.Values)
            if (session.RemoteEndPoint?.Address.ToString() == ipAddress)
                builder.Add(session);

        return builder.ToImmutable();
    }

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

        lock (_associationLock)
        {
            if (_sessionToAccount.TryRemove(sessionId, out var accountId))
                _accountToSession.TryRemove(new KeyValuePair<long, long>(accountId, sessionId));
        }
    }

    public ClientSession? AssociateAccount(long sessionId, long accountId)
    {
        ClientSession? evicted = null;

        lock (_associationLock)
        {
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

        if (evicted is not null)
            logger?.LogWarning(
                "Account {AccountId}: evicting session {EvictedSessionId} in favor of session {SessionId}",
                accountId, evicted.SessionId, sessionId);

        evicted?.Abort(DisconnectReason.Evicted);
        return evicted;
    }
}
