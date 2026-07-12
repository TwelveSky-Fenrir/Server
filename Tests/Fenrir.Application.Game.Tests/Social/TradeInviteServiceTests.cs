using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Social;

public class TradeInviteServiceTests
{
    private static (TradeInviteService Service, ZoneRegistry Zones, TradeRegistry Trades) CreateService(short mapId,
        DuelRegistry? duels = null, FriendRegistry? friends = null, PartyRegistry? parties = null,
        MentorRegistry? mentors = null, GuildInviteRegistry? guildInvites = null,
        ICharacterShardLocationRepository? directory = null, FakeSocialCrossShardRelayQueue? relay = null)
    {
        var trades = new TradeRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([mapId]);
        var worldState = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        return (new TradeInviteService(zones, trades, duels ?? new DuelRegistry(), friends ?? new FriendRegistry(),
            parties ?? new PartyRegistry(), mentors ?? new MentorRegistry(),
            guildInvites ?? new GuildInviteRegistry(), worldState,
            directory ?? new FakeCharacterShardLocationRepository(),
            relay ?? new FakeSocialCrossShardRelayQueue(), Options.Create(new GameServerOptions { ShardId = 1 }),
            NullLogger<TradeInviteService>.Instance), zones, trades);
    }

    private static PlayerRuntimeState Enter(ZoneRegistry zones, short mapId, int characterId, string name,
        byte tribe = 1)
    {
        zones.TryGet(mapId, out var zone);
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone!.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId, name, tribe: tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return state!;
    }

    [Fact]
    public async Task Invite_AskerBusy_AndTargetNameDoesNotExist_ReturnsAskerBusy_NotTargetNotFound()
    {
        var (service, zones, trades) = CreateService(1);
        var asker = Enter(zones, 1, 1, "Asker");
        Enter(zones, 1, 2, "PendingTarget");

        Assert.Equal(TradeAskOutcome.Sent, trades.TryAsk(1, 2));

        var result = await service.InviteAsync(zones[1], asker, "NoSuchAvatar", CancellationToken.None);

        Assert.Equal(TradeInviteResultKind.AskerBusy, result.Kind);
    }

    [Fact]
    public async Task Invite_AskerNotBusy_TargetNameDoesNotExist_ReturnsTargetNotFound()
    {
        var (service, zones, _) = CreateService(1);
        var asker = Enter(zones, 1, 1, "Asker");

        var result = await service.InviteAsync(zones[1], asker, "NoSuchAvatar", CancellationToken.None);

        Assert.Equal(TradeInviteResultKind.TargetNotFound, result.Kind);
    }

    [Fact]
    public async Task Invite_Success_SendsAskersCombinedLevel_NotOrdinaryLevelAlone()
    {
        var (service, zones, _) = CreateService(1);
        var asker = Enter(zones, 1, 1, "Asker");
        asker.Level = 30;
        asker.Level2 = 7;
        Enter(zones, 1, 2, "Target");

        var result = await service.InviteAsync(zones[1], asker, "Target", CancellationToken.None);

        Assert.Equal(TradeInviteResultKind.Sent, result.Kind);
        Assert.Equal(37, result.AskerLevel);
    }

    [Fact]
    public async Task Invite_SameShardMiss_ResolvesCrossShard_PublishesAskAndReturnsSentCrossShard()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, 9, 77, "RemoteTarget", 1, DateTime.UtcNow));
        var relay = new FakeSocialCrossShardRelayQueue();
        var (service, zones, trades) = CreateService(1, directory: directory, relay: relay);
        var asker = Enter(zones, 1, 1, "Asker");

        var result = await service.InviteAsync(zones[1], asker, "RemoteTarget", CancellationToken.None);

        Assert.Equal(TradeInviteResultKind.SentCrossShard, result.Kind);
        Assert.Single(relay.Enqueued);
        Assert.Equal(SocialCrossShardRelayKind.Trade, relay.Enqueued[0].Kind);
        Assert.True(trades.IsBusy(1));
    }
}
