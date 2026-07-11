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

public class MentorAskServiceTests
{
    private const short MasterLevel = 120;

    private static (MentorAskService Service, ZoneRegistry Zones, MentorRegistry Mentors) CreateService(short mapId)
    {
        return CreateService(mapId, new FakeCharacterShardLocationRepository(), new FakeSocialCrossShardRelayQueue());
    }

    private static (MentorAskService Service, ZoneRegistry Zones, MentorRegistry Mentors) CreateService(short mapId,
        ICharacterShardLocationRepository directory, FakeSocialCrossShardRelayQueue relay)
    {
        var mentors = new MentorRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([mapId]);
        return (new MentorAskService(mentors, new DuelRegistry(), new TradeRegistry(), new FriendRegistry(),
                new PartyRegistry(), new GuildInviteRegistry(), directory, relay,
                Options.Create(new GameServerOptions { ShardId = 1 }), new CapturingLogger<MentorAskService>()),
            zones, mentors);
    }

    private static PlayerRuntimeState Enter(ZoneRegistry zones, short mapId, int characterId, string name,
        byte tribe = 1, short level = MasterLevel)
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
    public async Task Ask_MasterBusy_AndTargetNameDoesNotExist_ReturnsAskerBusy_NotTargetNotFound()
    {
        var (service, zones, mentors) = CreateService(1);
        var master = Enter(zones, 1, 1, "Master");
        Enter(zones, 1, 2, "PendingStudent", level: 1);

        Assert.Equal(MentorAskOutcome.Sent, mentors.TryAsk(1, 2, false, false));

        var result = await service.AskAsync(zones[1], master, "NoSuchAvatar", CancellationToken.None);

        Assert.Equal(MentorAskResultKind.AskerBusy, result.Kind);
    }

    [Fact]
    public async Task Ask_MasterNotBusy_TargetNameDoesNotExist_ReturnsTargetNotFound()
    {
        var (service, zones, _) = CreateService(1);
        var master = Enter(zones, 1, 1, "Master");

        var result = await service.AskAsync(zones[1], master, "NoSuchAvatar", CancellationToken.None);

        Assert.Equal(MentorAskResultKind.TargetNotFound, result.Kind);
    }

    [Fact]
    public async Task Ask_MasterLevelTooLow_ReturnsAskerMustDisconnect_BeforeBusyOrTargetChecks()
    {
        var (service, zones, mentors) = CreateService(1);
        var master = Enter(zones, 1, 1, "Master", level: 50);
        Enter(zones, 1, 2, "PendingStudent", level: 1);
        Assert.Equal(MentorAskOutcome.Sent, mentors.TryAsk(1, 2, false, false));

        var result = await service.AskAsync(zones[1], master, "NoSuchAvatar", CancellationToken.None);

        Assert.Equal(MentorAskResultKind.AskerMustDisconnect, result.Kind);
    }

    [Fact]
    public async Task Ask_SameShardMiss_ResolvesCrossShard_PublishesAskAndReturnsSentCrossShard()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, 9, 77, "RemoteStudent", 1, DateTime.UtcNow));
        var relay = new FakeSocialCrossShardRelayQueue();
        var (service, zones, mentors) = CreateService(1, directory, relay);
        var master = Enter(zones, 1, 1, "Master");

        var result = await service.AskAsync(zones[1], master, "RemoteStudent", CancellationToken.None);

        Assert.Equal(MentorAskResultKind.SentCrossShard, result.Kind);
        Assert.Single(relay.Enqueued);
        Assert.Equal(SocialCrossShardRelayKind.Mentor, relay.Enqueued[0].Kind);
        Assert.True(mentors.IsNegotiating(1));
    }
}
