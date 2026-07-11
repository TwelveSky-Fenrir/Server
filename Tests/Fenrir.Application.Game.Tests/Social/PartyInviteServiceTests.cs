using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Social;

public class PartyInviteServiceTests
{
    private static (PartyInviteService Service, ZoneRegistry Zones, PartyRegistry Parties) CreateService(short mapId)
    {
        return CreateService(mapId, new FakeCharacterShardLocationRepository(), new FakeSocialCrossShardRelayQueue());
    }

    private static (PartyInviteService Service, ZoneRegistry Zones, PartyRegistry Parties) CreateService(short mapId,
        ICharacterShardLocationRepository directory, FakeSocialCrossShardRelayQueue relay)
    {
        var parties = new PartyRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([mapId]);
        return (new PartyInviteService(parties, new DuelRegistry(), new TradeRegistry(), new FriendRegistry(),
                new MentorRegistry(), new GuildInviteRegistry(), directory, relay,
                Options.Create(new GameServerOptions { ShardId = 1 }), new CapturingLogger<PartyInviteService>()),
            zones, parties);
    }

    private static PlayerRuntimeState Enter(ZoneRegistry zones, short mapId, int characterId, string name,
        byte tribe = 1, short level = 10)
    {
        zones.TryGet(mapId, out var zone);
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone!.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, mapId, name, tribe: tribe, level: level)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return state!;
    }

    [Fact]
    public async Task Invite_InviterAlreadyPartiedAndNotLeader_AndTargetNameDoesNotExist_ReturnsInviterMustDisconnect()
    {
        var (service, zones, parties) = CreateService(1);
        Enter(zones, 1, 1, "Leader");
        var inviter = Enter(zones, 1, 2, "Member");

        Assert.Equal(PartyInviteOutcome.Sent, parties.TryInvite(1, 10, 1, 2, 10, 1));
        Assert.True(parties.TryAnswer(2, true, out _, out _));

        var result = await service.InviteAsync(zones[1], inviter, "NoSuchAvatar", CancellationToken.None);

        Assert.Equal(PartyInviteResultKind.InviterMustDisconnect, result.Kind);
    }

    [Fact]
    public async Task Invite_InviterBusy_AndTargetNameDoesNotExist_ReturnsInviterBusy_NotTargetNotFound()
    {
        var (service, zones, parties) = CreateService(1);
        var inviter = Enter(zones, 1, 1, "Inviter");
        Enter(zones, 1, 2, "PendingTarget");

        Assert.Equal(PartyInviteOutcome.Sent, parties.TryInvite(1, 10, 1, 2, 10, 1));

        var result = await service.InviteAsync(zones[1], inviter, "NoSuchAvatar", CancellationToken.None);

        Assert.Equal(PartyInviteResultKind.InviterBusy, result.Kind);
    }

    [Fact]
    public async Task Invite_InviterNotBusy_TargetNameDoesNotExist_ReturnsTargetNotFound()
    {
        var (service, zones, _) = CreateService(1);
        var inviter = Enter(zones, 1, 1, "Inviter");

        var result = await service.InviteAsync(zones[1], inviter, "NoSuchAvatar", CancellationToken.None);

        Assert.Equal(PartyInviteResultKind.TargetNotFound, result.Kind);
    }

        [Fact]
    public async Task Invite_CombinedLevelGapWithinTolerance_Sends_EvenThoughOrdinaryLevelGapAlone_ExceedsIt()
    {
        var (service, zones, _) = CreateService(1);
        var inviter = Enter(zones, 1, 1, "Inviter", level: 30);
        var target = Enter(zones, 1, 2, "Target", level: 10);
        target.Level2 = 15;

        var result = await service.InviteAsync(zones[1], inviter, "Target", CancellationToken.None);

        Assert.Equal(PartyInviteResultKind.Sent, result.Kind);
    }

    [Fact]
    public async Task Invite_CombinedLevelGapExceedsTolerance_MustDisconnect_EvenThoughOrdinaryLevelGapAlone_DoesNot()
    {
        var (service, zones, _) = CreateService(1);
        var inviter = Enter(zones, 1, 1, "Inviter", level: 10);
        var target = Enter(zones, 1, 2, "Target", level: 9);
        target.Level2 = 15;

        var result = await service.InviteAsync(zones[1], inviter, "Target", CancellationToken.None);

        Assert.Equal(PartyInviteResultKind.InviterMustDisconnect, result.Kind);
    }

        [Fact]
    public async Task Invite_SameShardMiss_ResolvesCrossShard_PublishesAskAndReturnsSentCrossShard()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, 9, 77, "RemoteTarget", 1, DateTime.UtcNow));
        var relay = new FakeSocialCrossShardRelayQueue();
        var (service, zones, parties) = CreateService(1, directory, relay);
        var inviter = Enter(zones, 1, 1, "Inviter");

        var result = await service.InviteAsync(zones[1], inviter, "RemoteTarget", CancellationToken.None);

        Assert.Equal(PartyInviteResultKind.SentCrossShard, result.Kind);
        Assert.Single(relay.Enqueued);
        var entry = relay.Enqueued[0];
        Assert.Equal(SocialCrossShardRelayKind.Party, entry.Kind);
        Assert.Equal(SocialCrossShardRelayMessageType.Ask, entry.MessageType);
        Assert.Equal(1, entry.SourceCharacterId);
        Assert.Equal((byte)9, entry.TargetShardId);
        Assert.Equal(2, entry.TargetCharacterId);
        Assert.True(parties.IsNegotiating(1));
    }

        [Fact]
    public async Task Invite_SameShardMiss_ResolvesCrossShard_TribeMismatch_ReturnsInviterMustDisconnect()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, 9, 77, "RemoteTarget", 2, DateTime.UtcNow));
        var relay = new FakeSocialCrossShardRelayQueue();
        var (service, zones, _) = CreateService(1, directory, relay);
        var inviter = Enter(zones, 1, 1, "Inviter", 1);

        var result = await service.InviteAsync(zones[1], inviter, "RemoteTarget", CancellationToken.None);

        Assert.Equal(PartyInviteResultKind.InviterMustDisconnect, result.Kind);
        Assert.Empty(relay.Enqueued);
    }
}
