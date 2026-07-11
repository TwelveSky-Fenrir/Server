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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Social;

public class GuildInviteServiceTests
{

        private static (ZoneRegistry Zones, PlayerRuntimeState Asker, PlayerRuntimeState Target) MakeAskerAndTarget(
        byte tribe = 1, short mapId = 5)
    {
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([mapId]);
        zones.TryGet(mapId, out var zone);

        var (askerSession, _) = ZoneTestKit.CreateSession(1);
        zone!.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(askerSession, mapId, "Asker", tribe: tribe)));

        var (targetSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(targetSession, mapId, "Target", tribe: tribe)));

        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.TryGetPlayer(1, out var asker);
        zone.TryGetPlayer(2, out var target);
        Assert.NotNull(asker);
        Assert.NotNull(target);

        asker!.GuildId = 10;
        asker.GuildRoleDb = 2;

        return (zones, asker, target!);
    }

    private static GuildInviteService CreateService(ZoneRegistry zones, DuelRegistry? duels = null,
        TradeRegistry? trades = null, FriendRegistry? friends = null, PartyRegistry? parties = null,
        MentorRegistry? mentors = null, ICharacterShardLocationRepository? directory = null,
        FakeSocialCrossShardRelayQueue? relay = null)
    {
        return new GuildInviteService(zones, new GuildInviteRegistry(),
            duels ?? new DuelRegistry(), trades ?? new TradeRegistry(), friends ?? new FriendRegistry(),
            parties ?? new PartyRegistry(), mentors ?? new MentorRegistry(),
            directory ?? new FakeCharacterShardLocationRepository(), relay ?? new FakeSocialCrossShardRelayQueue(),
            Options.Create(new GameServerOptions { ShardId = 1 }), NullLogger<GuildInviteService>.Instance);
    }

    [Fact]
    public async Task Ask_NeitherSideBusy_Sends()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        var service = CreateService(zones);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.Sent, result);
    }

        [Fact]
    public async Task Ask_TargetOnDifferentMapSameShard_Sends()
    {
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([5, 6]);

        zones.TryGet(5, out var askerZone);
        var (askerSession, _) = ZoneTestKit.CreateSession(1);
        askerZone!.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(askerSession, 5, "Asker")));
        askerZone.Tick(TimeSpan.FromMilliseconds(50));
        askerZone.TryGetPlayer(1, out var asker);
        Assert.NotNull(asker);
        asker!.GuildId = 10;
        asker.GuildRoleDb = 2;

        zones.TryGet(6, out var targetZone);
        var (targetSession, _) = ZoneTestKit.CreateSession(2);
        targetZone!.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(targetSession, 6, "Target")));
        targetZone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(targetZone.TryGetPlayer(2, out _));

        var service = CreateService(zones);

        var result = await service.AskAsync(asker, "Target", CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.Sent, result);
    }

    [Fact]
    public async Task Ask_AskerHasOpenPersonalShop_ReturnsAskerBusy_WithoutResolvingTarget()
    {
        var (zones, asker, _) = MakeAskerAndTarget();
        asker.PshopOpen = true;
        var service = CreateService(zones);

        var result = await service.AskAsync(asker, "NoSuchAvatar", CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.AskerBusy, result);
    }

    [Fact]
    public async Task Ask_AskerStunned_ReturnsAskerBusy()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        asker.IsStunned = true;
        var service = CreateService(zones);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.AskerBusy, result);
    }

    [Fact]
    public async Task Ask_AskerDead_ReturnsAskerBusy()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        asker.IsDead = true;
        var service = CreateService(zones);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.AskerBusy, result);
    }

    [Fact]
    public async Task Ask_AskerNegotiatingADuel_ReturnsAskerBusy()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        var duels = new DuelRegistry();
        duels.TryAsk(asker.CharacterId, 999, false);
        var service = CreateService(zones, duels);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.AskerBusy, result);
    }

    [Fact]
    public async Task Ask_TargetNegotiatingATrade_ReturnsTargetBusy()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        var trades = new TradeRegistry();
        trades.TryAsk(target.CharacterId, 999);
        var service = CreateService(zones, trades: trades);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

    [Fact]
    public async Task Ask_TargetHasPendingFriendRequest_ReturnsTargetBusy()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        var friends = new FriendRegistry();
        friends.TryAsk(999, target.CharacterId);
        var service = CreateService(zones, friends: friends);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

    [Fact]
    public async Task Ask_TargetHasPendingPartyInvite_ReturnsTargetBusy()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        var parties = new PartyRegistry();
        parties.TryInvite(999, 1, target.Tribe, target.CharacterId, 1, target.Tribe);
        var service = CreateService(zones, parties: parties);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

    [Fact]
    public async Task Ask_TargetNegotiatingAsAMentor_ReturnsTargetBusy()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        var mentors = new MentorRegistry();
        mentors.TryAsk(target.CharacterId, 999, false, false);
        var service = CreateService(zones, mentors: mentors);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

    [Fact]
    public async Task Ask_TargetStunned_ReturnsTargetBusy()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        target.IsStunned = true;
        var service = CreateService(zones);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

    [Fact]
    public async Task Ask_TargetDead_ReturnsTargetBusy()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        target.IsDead = true;
        var service = CreateService(zones);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

        [Fact]
    public async Task Ask_TargetMovingZone_ReturnsTargetBusy()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        target.IsMovingZone = true;
        var service = CreateService(zones);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

        [Fact]
    public async Task Ask_AskerMovingZone_DoesNotBlockAsk()
    {
        var (zones, asker, target) = MakeAskerAndTarget();
        asker.IsMovingZone = true;
        var service = CreateService(zones);

        var result = await service.AskAsync(asker, target.Name, CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.Sent, result);
    }

        [Fact]
    public async Task Ask_SameShardMiss_ResolvesCrossShard_PublishesAskAndReturnsSentCrossShard()
    {
        var (zones, asker, _) = MakeAskerAndTarget();
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(3, 9, 77, "RemoteTarget", asker.Tribe, DateTime.UtcNow));
        var relay = new FakeSocialCrossShardRelayQueue();
        var service = CreateService(zones, directory: directory, relay: relay);

        var result = await service.AskAsync(asker, "RemoteTarget", CancellationToken.None);

        Assert.Equal(GuildInviteAskResultKind.SentCrossShard, result);
        Assert.Single(relay.Enqueued);
        Assert.Equal(SocialCrossShardRelayKind.GuildInvite, relay.Enqueued[0].Kind);
    }
}
