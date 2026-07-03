using Fenrir.Network.Sessions;

namespace Fenrir.Network.Tests.Sessions;

/// <summary>
///     <c>SessionRegistry</c> (§8.1): a per-server <c>SessionId → ClientSession</c> map plus a secondary
///     <c>AccountId → SessionId</c> index enforcing one live connection per account — the new connection wins,
///     the old one is torn down with <see cref="DisconnectReason.Evicted" />.
/// </summary>
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

    // New connection for an already-associated account evicts the old one — "one connection per account".
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

    private static ZoneClientSession NewSession(long sessionId)
    {
        return new ZoneClientSession(sessionId, new FakeDuplexPipe());
    }
}
