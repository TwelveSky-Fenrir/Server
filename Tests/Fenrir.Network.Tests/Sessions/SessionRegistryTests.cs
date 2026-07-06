using Fenrir.Network.Dispatch.Sessions;

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

    private static ZoneClientSession NewSession(long sessionId)
    {
        return new ZoneClientSession(sessionId, new FakeDuplexPipe());
    }
}
