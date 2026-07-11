using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Social;

public class PartyResyncRelayHandlerTests
{
    private const byte OwnShardId = 3;
    private const byte OtherShardId = 7;

    private static (PartyResyncRelayHandler Handler, ZoneRegistry Zones, PartyRegistry Parties,
        FakePartyResyncRelayQueue Relay) CreateHandler()
    {
        var parties = new PartyRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([1]);
        var relay = new FakePartyResyncRelayQueue();
        var handler = new PartyResyncRelayHandler(zones, parties, new Lazy<IPartyResyncRelayQueue>(() => relay),
            Options.Create(new GameServerOptions { ShardId = OwnShardId }),
            NullLogger<PartyResyncRelayHandler>.Instance);
        return (handler, zones, parties, relay);
    }

    private static PlayerRuntimeState Enter(ZoneRegistry zones, short mapId, int characterId, string name)
    {
        zones.TryGet(mapId, out var zone);
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone!.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId, name)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return state!;
    }

    private static PartyResyncRelayDto MakeRequest(string partyName, string avatarName = "ReconnectingMember",
        int subjectCharacterId = 999, long relayId = 42)
    {
        return new PartyResyncRelayDto(relayId, (byte)PartyResyncRelaySort.Request, OtherShardId,
            subjectCharacterId, partyName, avatarName);
    }

    [Fact]
    public async Task HandleAsync_RequestForAHostedLeader_RepublishesPartyInfo_PreservingSubjectIdentity()
    {
        var (handler, zones, parties, relay) = CreateHandler();
        var leader = Enter(zones, 1, 501, "Leader");
        Enter(zones, 1, 502, "Follower");
        Assert.Equal(PartyInviteOutcome.Sent,
            parties.TryInvite(leader.CharacterId, 1, leader.Tribe, 502, 1, leader.Tribe));
        Assert.True(parties.TryAnswer(502, true, false, out _, out _, out _));

        var request = MakeRequest("Leader");
        await handler.HandleAsync(request, CancellationToken.None);

        var republished = Assert.Single(relay.Enqueued);
        Assert.Equal((byte)PartyResyncRelaySort.PartyInfoReply, republished.Sort);
        Assert.Equal(OwnShardId, republished.SourceShardId);
        Assert.Equal(999, republished.SourceCharacterId);
        Assert.Equal("Leader", republished.PartyName);
        Assert.Equal("ReconnectingMember", republished.AvatarName);
    }

    [Fact]
    public async Task HandleAsync_NoOnlineCharacterWithThatName_IsANoOp()
    {
        var (handler, _, _, relay) = CreateHandler();

        await handler.HandleAsync(MakeRequest("Nobody"), CancellationToken.None);

        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task HandleAsync_NamedCharacterOnlineButNeverPartied_IsANoOp()
    {
        var (handler, zones, _, relay) = CreateHandler();
        Enter(zones, 1, 501, "NotALeader");

        await handler.HandleAsync(MakeRequest("NotALeader"), CancellationToken.None);

        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task HandleAsync_NamedCharacterIsAnOrdinaryMemberNotTheLeader_IsANoOp()
    {
        var (handler, zones, parties, relay) = CreateHandler();
        var leader = Enter(zones, 1, 501, "Leader");
        var follower = Enter(zones, 1, 502, "Follower");
        Assert.Equal(PartyInviteOutcome.Sent,
            parties.TryInvite(leader.CharacterId, 1, leader.Tribe, follower.CharacterId, 1, leader.Tribe));
        Assert.True(parties.TryAnswer(follower.CharacterId, true, false, out _, out _, out _));

        await handler.HandleAsync(MakeRequest("Follower"), CancellationToken.None);

        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task HandleAsync_PartyBreakSort_IsANoOp_DoesNotThrow()
    {
        var (handler, zones, parties, relay) = CreateHandler();
        var leader = Enter(zones, 1, 501, "Leader");
        Enter(zones, 1, 502, "Follower");
        parties.TryInvite(leader.CharacterId, 1, leader.Tribe, 502, 1, leader.Tribe);
        parties.TryAnswer(502, true, false, out _, out _, out _);

        var breakRow = new PartyResyncRelayDto(1, (byte)PartyResyncRelaySort.PartyBreak, OtherShardId, 999,
            "Leader", "ReconnectingMember");

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(breakRow, CancellationToken.None).AsTask());

        Assert.Null(ex);
        Assert.Empty(relay.Enqueued);
    }
}
