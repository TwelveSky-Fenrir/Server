using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Social;

public class FriendCrossShardRelayHandlerTests
{
    private const byte OwnShardId = 3;

    private static (FriendCrossShardRelayHandler Handler, ZoneRegistry Zones, FriendRegistry Friends,
        FakeSocialCrossShardRelayQueue Relay) CreateHandler()
    {
        var friends = new FriendRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([1]);
        var relay = new FakeSocialCrossShardRelayQueue();
        var handler = new FriendCrossShardRelayHandler(zones, friends, new DuelRegistry(), new TradeRegistry(),
            new PartyRegistry(), new GuildInviteRegistry(), new MentorRegistry(),
            new Lazy<ISocialCrossShardRelayQueue>(() => relay),
            Options.Create(new GameServerOptions { ShardId = OwnShardId }),
            NullLogger<FriendCrossShardRelayHandler>.Instance);
        return (handler, zones, friends, relay);
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
        return new SocialCrossShardRelayDto(relayId, (byte)SocialCrossShardRelayKind.Friend,
            (byte)SocialCrossShardRelayMessageType.Ask, null, null, 7, 200, "RemoteAsker", OwnShardId,
            targetCharacterId, null);
    }

    [Fact]
    public async Task HandleAskAsync_TargetNotFoundLocally_PublishesDeclineWithReasonCode4()
    {
        var (handler, _, _, relay) = CreateHandler();

        await handler.HandleAskAsync(MakeAsk(), CancellationToken.None);

        var declined = Assert.Single(relay.Enqueued);
        Assert.Equal(SocialCrossShardRelayMessageType.Answer, declined.MessageType);
        Assert.False(declined.Accepted);
        Assert.Equal((byte)4, declined.ReasonCode);
    }

    [Fact]
    public async Task HandleAskAsync_TargetAlreadyNegotiating_PublishesDeclineWithReasonCode5()
    {
        var (handler, zones, friends, relay) = CreateHandler();
        var (target, _) = Enter(zones, 1, 100, "Target");
        friends.TryAsk(target.CharacterId, 999);

        await handler.HandleAskAsync(MakeAsk(targetCharacterId: 100), CancellationToken.None);

        var declined = Assert.Single(relay.Enqueued);
        Assert.False(declined.Accepted);
        Assert.Equal((byte)5, declined.ReasonCode);
    }

    [Fact]
    public async Task HandleAskAsync_TargetAvailable_RegistersInboundAndSendsPrompt()
    {
        var (handler, zones, friends, relay) = CreateHandler();
        var (target, pipe) = Enter(zones, 1, 100, "Target");

        await handler.HandleAskAsync(MakeAsk(42), CancellationToken.None);

        Assert.Empty(relay.Enqueued);
        Assert.True(friends.IsNegotiating(target.CharacterId));
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task HandleAnswerAsync_NoPendingOutbound_IsANoOp_DoesNotThrow()
    {
        var (handler, _, _, relay) = CreateHandler();

        var answer = new SocialCrossShardRelayDto(1, (byte)SocialCrossShardRelayKind.Friend,
            (byte)SocialCrossShardRelayMessageType.Answer, true, null, 7, 200, "RemoteTarget", OwnShardId, 100, 42);

        var ex = await Record.ExceptionAsync(() => handler.HandleAnswerAsync(answer, CancellationToken.None).AsTask());

        Assert.Null(ex);
        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task HandleAnswerAsync_Accepted_NotifiesAskerAndMarksAcceptedForTheAskerSide()
    {
        var (handler, zones, friends, _) = CreateHandler();
        var (asker, pipe) = Enter(zones, 1, 100, "Asker");
        Assert.Equal(FriendAskOutcome.Sent,
            friends.TryAskCrossShard(asker.CharacterId, new CrossShardOutboundAsk(7, 200, "RemoteTarget")));

        var answer = new SocialCrossShardRelayDto(1, (byte)SocialCrossShardRelayKind.Friend,
            (byte)SocialCrossShardRelayMessageType.Answer, true, null, 7, 200, "RemoteTarget", OwnShardId, 100, 42);

        await handler.HandleAnswerAsync(answer, CancellationToken.None);

        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
        Assert.True(friends.IsNegotiating(asker.CharacterId));
        Assert.True(friends.TryConsumeAccepted(asker.CharacterId, out var otherId));
        Assert.Equal(200, otherId);
    }

    [Fact]
    public async Task HandleAnswerAsync_Declined_ClearsOutboundBusyState_WithoutMarkingAccepted()
    {
        var (handler, zones, friends, _) = CreateHandler();
        var (asker, _) = Enter(zones, 1, 100, "Asker");
        friends.TryAskCrossShard(asker.CharacterId, new CrossShardOutboundAsk(7, 200, "RemoteTarget"));

        var answer = new SocialCrossShardRelayDto(1, (byte)SocialCrossShardRelayKind.Friend,
            (byte)SocialCrossShardRelayMessageType.Answer, false, 5, 7, 200, "RemoteTarget", OwnShardId, 100, 42);

        await handler.HandleAnswerAsync(answer, CancellationToken.None);

        Assert.False(friends.IsNegotiating(asker.CharacterId));
        Assert.False(friends.TryConsumeAccepted(asker.CharacterId, out _));
    }
}
