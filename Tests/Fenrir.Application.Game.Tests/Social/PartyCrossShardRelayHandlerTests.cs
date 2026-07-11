using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Social;

public class PartyCrossShardRelayHandlerTests
{
    private const byte OwnShardId = 3;

    private static (PartyCrossShardRelayHandler Handler, ZoneRegistry Zones, PartyRegistry Parties,
        FakeSocialCrossShardRelayQueue Relay) CreateHandler()
    {
        var parties = new PartyRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([1]);
        var relay = new FakeSocialCrossShardRelayQueue();
        var handler = new PartyCrossShardRelayHandler(zones, parties,
            new Lazy<ISocialCrossShardRelayQueue>(() => relay),
            Options.Create(new GameServerOptions { ShardId = OwnShardId }),
            NullLogger<PartyCrossShardRelayHandler>.Instance);
        return (handler, zones, parties, relay);
    }

        private static (PlayerRuntimeState State, FakeDuplexPipe Pipe) Enter(ZoneRegistry zones, short mapId,
        int characterId, string name)
    {
        zones.TryGet(mapId, out var zone);
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        zone!.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId, name)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        ZoneTestKit.DrainOutbound(pipe);
        return (state!, pipe);
    }

    private static SocialCrossShardRelayDto MakeAsk(long relayId = 1, int targetCharacterId = 100)
    {
        return new SocialCrossShardRelayDto(relayId, (byte)SocialCrossShardRelayKind.Party,
            (byte)SocialCrossShardRelayMessageType.Ask, null, null, 7, 200, "RemoteInviter", OwnShardId,
            targetCharacterId, null);
    }

    [Fact]
    public async Task HandleAskAsync_TargetNotFoundLocally_PublishesDeclineWithReasonCode4()
    {
        var (handler, _, _, relay) = CreateHandler();

        await handler.HandleAskAsync(MakeAsk(), CancellationToken.None);

        var declined = Assert.Single(relay.Enqueued);
        Assert.False(declined.Accepted);
        Assert.Equal((byte)4, declined.ReasonCode);
    }

    [Fact]
    public async Task HandleAskAsync_TargetAvailable_RegistersInboundAndSendsPrompt()
    {
        var (handler, zones, parties, relay) = CreateHandler();
        var (invitee, pipe) = Enter(zones, 1, 100, "Invitee");

        await handler.HandleAskAsync(MakeAsk(42, 100), CancellationToken.None);

        Assert.Empty(relay.Enqueued);
        Assert.True(parties.IsNegotiating(invitee.CharacterId));
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task HandleAskAsync_TargetAlreadyPartied_PublishesDeclineWithReasonCode5()
    {
        var (handler, zones, parties, relay) = CreateHandler();
        var (invitee, _) = Enter(zones, 1, 100, "Invitee");

        Enter(zones, 1, 501, "Leader");
        Assert.Equal(PartyInviteOutcome.Sent, parties.TryInvite(501, 1, invitee.Tribe, 100, 1, invitee.Tribe));
        Assert.True(parties.TryAnswer(100, true, out _, out _));

        await handler.HandleAskAsync(MakeAsk(43, 100), CancellationToken.None);

        var declined = Assert.Single(relay.Enqueued);
        Assert.False(declined.Accepted);
        Assert.Equal((byte)5, declined.ReasonCode);
    }

    [Fact]
    public async Task HandleAnswerAsync_NoPendingOutbound_IsANoOp_DoesNotThrow()
    {
        var (handler, _, _, relay) = CreateHandler();

        var answer = new SocialCrossShardRelayDto(1, (byte)SocialCrossShardRelayKind.Party,
            (byte)SocialCrossShardRelayMessageType.Answer, true, null, 7, 200, "RemoteInvitee", OwnShardId, 100, 42);

        var ex = await Record.ExceptionAsync(() => handler.HandleAnswerAsync(answer, CancellationToken.None).AsTask());

        Assert.Null(ex);
        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task HandleAnswerAsync_Accepted_CreatesTheParty_OnTheInvitersOwnShard()
    {
        var (handler, zones, parties, _) = CreateHandler();
        var (inviter, pipe) = Enter(zones, 1, 100, "Inviter");
        Assert.Equal(PartyInviteOutcome.Sent,
            parties.TryInviteCrossShard(inviter.CharacterId, new CrossShardOutboundAsk(7, 200, "RemoteInvitee")));

        var answer = new SocialCrossShardRelayDto(1, (byte)SocialCrossShardRelayKind.Party,
            (byte)SocialCrossShardRelayMessageType.Answer, true, null, 7, 200, "RemoteInvitee", OwnShardId, 100, 42);

        await handler.HandleAnswerAsync(answer, CancellationToken.None);

        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
        Assert.True(parties.IsLeader(inviter.CharacterId));
        var members = parties.GetMembers(inviter.CharacterId);
        Assert.Equal([100, 200], members);
    }

    [Fact]
    public async Task HandleAnswerAsync_Declined_ClearsOutboundBusyState_WithoutCreatingAParty()
    {
        var (handler, zones, parties, _) = CreateHandler();
        var (inviter, _) = Enter(zones, 1, 100, "Inviter");
        parties.TryInviteCrossShard(inviter.CharacterId, new CrossShardOutboundAsk(7, 200, "RemoteInvitee"));

        var answer = new SocialCrossShardRelayDto(1, (byte)SocialCrossShardRelayKind.Party,
            (byte)SocialCrossShardRelayMessageType.Answer, false, 5, 7, 200, "RemoteInvitee", OwnShardId, 100, 42);

        await handler.HandleAnswerAsync(answer, CancellationToken.None);

        Assert.False(parties.IsNegotiating(inviter.CharacterId));
        Assert.False(parties.IsLeader(inviter.CharacterId));
    }
}
