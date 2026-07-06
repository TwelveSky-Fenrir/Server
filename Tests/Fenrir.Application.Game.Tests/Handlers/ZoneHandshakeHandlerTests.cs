using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Handlers;

// op11 ZC_TEMP_REGISTER_SEND -- cross-process duplicate-login kick/refusal plus the tribe-population quota
// gate, Game-side handler branch coverage.
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
        Assert.Equal((short)0, session.AccountGrade);
        Assert.False(session.IsGm);
        Assert.True(registry.TryGetByAccount(AccountId, out var associated));
        Assert.Same(session, associated);
        await PacketAssert.AssertSentAsync(pipe, new ZoneHandshakeResponse { Result = 0 });
    }

    // GM-BLOCK precondition: the account-grade fact carried in the ticket must land on the Zone session
    // itself, never re-queried per action.
    [Fact]
    public async Task HandleAsync_Accepted_WithGmGradeTicket_MarksSessionGmElevated()
    {
        var registry = new SessionRegistry();
        var handler = new ZoneHandshakeHandler(
            new StubZoneHandshakeService(new ZoneHandshakeResult(ZoneHandshakeOutcome.Accepted, AccountId,
                CharacterId, SessionToken, AccountGrade: 1)), registry);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry.Register(session);

        await handler.HandleAsync(new ZoneHandshakeRequest { Id = "irrelevant", Tribe = 0, UserSort = 0 }, session, CancellationToken.None);

        Assert.Equal((short)1, session.AccountGrade);
        Assert.True(session.IsGm);
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

    // Tribe-population quota gate: full quota is a normal, retry-able rejection -- same wire shape as a
    // rejected ticket (Result=1), never an abort.
    [Fact]
    public async Task HandleAsync_QuotaFull_SendsTheSameGenericFailure_AndDoesNotAbort()
    {
        var registry = new SessionRegistry();
        var handler = new ZoneHandshakeHandler(
            new StubZoneHandshakeService(new ZoneHandshakeResult(ZoneHandshakeOutcome.QuotaFull)), registry);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        await handler.HandleAsync(new ZoneHandshakeRequest { Id = "irrelevant", Tribe = 1, UserSort = 0 }, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new ZoneHandshakeResponse { Result = 1 });
    }

    // Protocol violation (invalid account index, or declared tribe outside the current map group's valid
    // range) -- silent drop, no response at all, same posture as CreateAvatarHandler's malformed-input guard.
    [Fact]
    public async Task HandleAsync_ProtocolViolation_AbortsSilently_NoResponseSent()
    {
        var registry = new SessionRegistry();
        var handler = new ZoneHandshakeHandler(
            new StubZoneHandshakeService(new ZoneHandshakeResult(ZoneHandshakeOutcome.ProtocolViolation)), registry);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        await handler.HandleAsync(new ZoneHandshakeRequest { Id = "irrelevant", Tribe = 9, UserSort = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        Assert.Equal([], ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task HandleAsync_PassesPacketTribeAndTheZoneSessionThroughToTheService()
    {
        var registry = new SessionRegistry();
        var spy = new SpyZoneHandshakeService(new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected));
        var handler = new ZoneHandshakeHandler(spy, registry);
        var (session, _) = ZoneTestKit.CreateSession(1);

        await handler.HandleAsync(new ZoneHandshakeRequest { Id = "MG7", Tribe = 2, UserSort = 5 }, session,
            CancellationToken.None);

        Assert.Equal("MG7", spy.LastObfuscatedId);
        Assert.Equal(2, spy.LastDeclaredTribe);
        Assert.Same(session, spy.LastSession);
    }

    private sealed class StubZoneHandshakeService(ZoneHandshakeResult result) : IZoneHandshakeService
    {
        public ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId, int declaredTribe,
            ZoneClientSession session, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyZoneHandshakeService(ZoneHandshakeResult result) : IZoneHandshakeService
    {
        public string? LastObfuscatedId { get; private set; }
        public int LastDeclaredTribe { get; private set; }
        public ZoneClientSession? LastSession { get; private set; }

        public ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId, int declaredTribe,
            ZoneClientSession session, CancellationToken cancellationToken)
        {
            LastObfuscatedId = obfuscatedId;
            LastDeclaredTribe = declaredTribe;
            LastSession = session;
            return ValueTask.FromResult(result);
        }
    }
}
