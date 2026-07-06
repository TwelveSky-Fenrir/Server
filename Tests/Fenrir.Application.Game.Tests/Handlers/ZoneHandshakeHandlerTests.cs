using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Handlers;

// op11 ZC_TEMP_REGISTER_SEND -- cross-process duplicate-login kick/refusal, Game-side handler branch coverage.
public class ZoneHandshakeHandlerTests
{
    private const int AccountId = 7;
    private const int CharacterId = 501;
    private static readonly Guid SessionToken = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_Accepted_MarksTicketConsumedWithToken_AssociatesAccount_AndReplies()
    {
        var registry = new SessionRegistry();
        var handler = new ZoneHandshakeHandler(
            new StubZoneHandshakeService(new ZoneHandshakeResult(ZoneHandshakeOutcome.Accepted, AccountId,
                CharacterId, SessionToken)), registry);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry.Register(session);

        await handler.HandleAsync(new ZoneHandshakeRequest { Id = "irrelevant", Tribe = 0, UserSort = 0 }, session, CancellationToken.None);

        Assert.Equal(AccountId, session.AccountId);
        Assert.Equal(CharacterId, session.CharacterId);
        Assert.Equal(SessionToken, session.AccountSessionToken);
        Assert.True(registry.TryGetByAccount(AccountId, out var associated));
        Assert.Same(session, associated);
        await PacketAssert.AssertSentAsync(pipe, new ZoneHandshakeResponse { Result = 0 });
    }

    [Fact]
    public async Task HandleAsync_SessionSuperseded_AbortsSilently_NoResponseSent_AndDoesNotAssociateAccount()
    {
        var registry = new SessionRegistry();
        var handler = new ZoneHandshakeHandler(
            new StubZoneHandshakeService(new ZoneHandshakeResult(ZoneHandshakeOutcome.SessionSuperseded)), registry);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        await handler.HandleAsync(new ZoneHandshakeRequest { Id = "irrelevant", Tribe = 0, UserSort = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Evicted, session.DisconnectReason);
        Assert.Null(session.AccountId);
        Assert.False(registry.TryGetByAccount(AccountId, out _));
        Assert.Equal([], ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task HandleAsync_Rejected_SendsGenericFailure_AndDoesNotAbort()
    {
        var registry = new SessionRegistry();
        var handler = new ZoneHandshakeHandler(
            new StubZoneHandshakeService(new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected)), registry);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        await handler.HandleAsync(new ZoneHandshakeRequest { Id = "irrelevant", Tribe = 0, UserSort = 0 }, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new ZoneHandshakeResponse { Result = 1 });
    }

    private sealed class StubZoneHandshakeService(ZoneHandshakeResult result) : IZoneHandshakeService
    {
        public ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(result);
        }
    }
}
