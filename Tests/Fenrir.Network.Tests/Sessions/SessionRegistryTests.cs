using System.Net;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Network.Tests.Sessions;

// SessionRegistry enforces one live connection per account: the new connection wins, the old one is torn
// down with DisconnectReason.Evicted.
public class SessionRegistryTests
{
    [Fact]
    public void Register_ThenTryGet_ReturnsTheSameSession()
    {
        var registry = new SessionRegistry();
        var session = NewSession(1);

        registry.Register(session);

        Assert.True(registry.TryGet(1, out var found));
        Assert.Same(session, found);
    }

    [Fact]
    public void TryGet_UnknownSessionId_ReturnsFalse()
    {
        var registry = new SessionRegistry();

        Assert.False(registry.TryGet(404, out var found));
        Assert.Null(found);
    }

    [Fact]
    public void Unregister_RemovesTheSession()
    {
        var registry = new SessionRegistry();
        var session = NewSession(1);
        registry.Register(session);

        registry.Unregister(1);

        Assert.False(registry.TryGet(1, out _));
    }

    [Fact]
    public void AssociateAccount_NewConnectionEvictsThePreviousSessionOfTheSameAccount()
    {
        var registry = new SessionRegistry();
        var oldSession = NewSession(1);
        var newSession = NewSession(2);
        registry.Register(oldSession);
        registry.Register(newSession);

        registry.AssociateAccount(oldSession.SessionId, 42);
        registry.AssociateAccount(newSession.SessionId, 42);

        Assert.Equal(DisconnectReason.Evicted, oldSession.DisconnectReason);
        Assert.Null(newSession.DisconnectReason);
    }

    [Fact]
    public void AssociateAccount_DifferentAccounts_DoNotEvictEachOther()
    {
        var registry = new SessionRegistry();
        var sessionA = NewSession(1);
        var sessionB = NewSession(2);
        registry.Register(sessionA);
        registry.Register(sessionB);

        registry.AssociateAccount(sessionA.SessionId, 1);
        registry.AssociateAccount(sessionB.SessionId, 2);

        Assert.Null(sessionA.DisconnectReason);
        Assert.Null(sessionB.DisconnectReason);
    }

    // TryGetByAccount/SnapshotAssociatedAccountIds back the cross-process duplicate-login kick/refusal
    // design: the Login-side eviction check and the per-shard liveness/kick poll both read through these.
    [Fact]
    public void TryGetByAccount_AfterAssociate_ReturnsTheAssociatedSession()
    {
        var registry = new SessionRegistry();
        var session = NewSession(1);
        registry.Register(session);

        registry.AssociateAccount(session.SessionId, 42);

        Assert.True(registry.TryGetByAccount(42, out var found));
        Assert.Same(session, found);
    }

    [Fact]
    public void TryGetByAccount_UnknownAccount_ReturnsFalse()
    {
        var registry = new SessionRegistry();

        Assert.False(registry.TryGetByAccount(404, out var found));
        Assert.Null(found);
    }

    [Fact]
    public void TryGetByAccount_AfterUnregister_NoLongerResolves()
    {
        var registry = new SessionRegistry();
        var session = NewSession(1);
        registry.Register(session);
        registry.AssociateAccount(session.SessionId, 42);

        registry.Unregister(session.SessionId);

        Assert.False(registry.TryGetByAccount(42, out _));
    }

    [Fact]
    public void SnapshotAssociatedAccountIds_ReflectsCurrentlyAssociatedAccountsOnly()
    {
        var registry = new SessionRegistry();
        var sessionA = NewSession(1);
        var sessionB = NewSession(2);
        registry.Register(sessionA);
        registry.Register(sessionB);
        registry.AssociateAccount(sessionA.SessionId, 10);
        registry.AssociateAccount(sessionB.SessionId, 20);

        var snapshot = registry.SnapshotAssociatedAccountIds();

        Assert.Contains(10L, snapshot);
        Assert.Contains(20L, snapshot);
        Assert.Equal(2, snapshot.Length);
    }

    [Fact]
    public void SnapshotAssociatedAccountIds_DropsAccountAfterUnregister()
    {
        var registry = new SessionRegistry();
        var session = NewSession(1);
        registry.Register(session);
        registry.AssociateAccount(session.SessionId, 10);

        registry.Unregister(session.SessionId);

        Assert.DoesNotContain(10L, registry.SnapshotAssociatedAccountIds());
    }

    // Backs IpFloodGuard's kick-matching-sessions step -- exact string equality only, mirroring legacy's own
    // IsMatchString/strcmp matching (no CIDR/prefix/wildcard matching).
    [Fact]
    public void SnapshotByRemoteAddress_ReturnsOnlySessionsWithThatExactAddress()
    {
        var registry = new SessionRegistry();
        var sharedIp = new IPEndPoint(IPAddress.Parse("203.0.113.10"), 1000);
        var sessionA = new ZoneClientSession(1, new FakeDuplexPipe(), sharedIp);
        var sessionB =
            new ZoneClientSession(2, new FakeDuplexPipe(), new IPEndPoint(IPAddress.Parse("203.0.113.10"), 2000));
        var otherSession = new ZoneClientSession(3, new FakeDuplexPipe(),
            new IPEndPoint(IPAddress.Parse("203.0.113.99"), 1000));
        registry.Register(sessionA);
        registry.Register(sessionB);
        registry.Register(otherSession);

        var snapshot = registry.SnapshotByRemoteAddress("203.0.113.10");

        Assert.Equal(2, snapshot.Length);
        Assert.Contains(sessionA, snapshot);
        Assert.Contains(sessionB, snapshot);
        Assert.DoesNotContain(otherSession, snapshot);
    }

    [Fact]
    public void SnapshotByRemoteAddress_NoSessionWithoutRemoteEndPoint_IsIncluded()
    {
        var registry = new SessionRegistry();
        var noEndPointSession = new ZoneClientSession(1, new FakeDuplexPipe());
        registry.Register(noEndPointSession);

        var snapshot = registry.SnapshotByRemoteAddress("203.0.113.10");

        Assert.Empty(snapshot);
    }

    [Fact]
    public void SnapshotByRemoteAddress_UnknownAddress_ReturnsEmpty()
    {
        var registry = new SessionRegistry();
        registry.Register(new ZoneClientSession(1, new FakeDuplexPipe(),
            new IPEndPoint(IPAddress.Parse("203.0.113.10"), 1000)));

        var snapshot = registry.SnapshotByRemoteAddress("198.51.100.1");

        Assert.Empty(snapshot);
    }

    // Backs the per-server idle-connection sweeps (GameServer's SessionLivenessSweep, Login's own
    // LoginSessionLivenessSweep) -- the comparison is deliberately strict `>`, not `>=`: a connection whose
    // silence exactly equals the threshold at the instant of a sweep pass is not yet idle, it is caught on a
    // later pass once the threshold is strictly exceeded (matches legacy's own comparison).
    [Fact]
    public void SnapshotIdle_ExcludesASessionExactlyAtTheThreshold()
    {
        var registry = new SessionRegistry();
        var session = NewSession(1);
        registry.Register(session);
        var idleTimeout = TimeSpan.FromSeconds(60);

        var snapshot = registry.SnapshotIdle(idleTimeout, session.LastActivityUtc + idleTimeout);

        Assert.Empty(snapshot);
    }

    [Fact]
    public void SnapshotIdle_IncludesASessionStrictlyPastTheThreshold()
    {
        var registry = new SessionRegistry();
        var session = NewSession(1);
        registry.Register(session);
        var idleTimeout = TimeSpan.FromSeconds(60);

        var snapshot =
            registry.SnapshotIdle(idleTimeout, session.LastActivityUtc + idleTimeout + TimeSpan.FromTicks(1));

        Assert.Contains(session, snapshot);
    }

    [Fact]
    public void SnapshotIdle_ExcludesASessionYoungerThanTheThreshold()
    {
        var registry = new SessionRegistry();
        var session = NewSession(1);
        registry.Register(session);
        var idleTimeout = TimeSpan.FromSeconds(60);

        var snapshot =
            registry.SnapshotIdle(idleTimeout, session.LastActivityUtc + idleTimeout - TimeSpan.FromTicks(1));

        Assert.Empty(snapshot);
    }

    private static ZoneClientSession NewSession(long sessionId)
    {
        return new ZoneClientSession(sessionId, new FakeDuplexPipe());
    }
}
