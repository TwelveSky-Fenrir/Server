using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Social;

/// <summary>
///     GUILD_ASK_SEND's CheckCommunityWork()/stunned-dead exclusivity gate (Server/ts25zone/S04_MyWork02.cpp:
///     9827-9884, Server/ts25zone/S07_MyGame04.cpp:185-216) -- <see cref="GuildInviteService.Ask" /> must reject
///     an asker or target who is busy in any OTHER community-interaction family, not just an existing guild
///     negotiation, and must reject either side while stunned or dead.
/// </summary>
public class GuildInviteServiceTests
{
    private static (Zone Zone, PlayerRuntimeState Asker, PlayerRuntimeState Target) MakeAskerAndTarget(
        byte tribe = 1)
    {
        var zone = ZoneTestKit.CreateZone(5);

        var (askerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(askerSession, 5, "Asker", tribe: tribe)));

        var (targetSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(targetSession, 5, "Target", tribe: tribe)));

        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.TryGetPlayer(1, out var asker);
        zone.TryGetPlayer(2, out var target);
        Assert.NotNull(asker);
        Assert.NotNull(target);

        asker.GuildId = 10;
        asker.GuildRoleDb = 2; // DB role 2 == master (GuildRoleCodec.IsMasterOrSubMaster)

        return (zone, asker, target);
    }

    private static GuildInviteService CreateService(DuelRegistry? duels = null, TradeRegistry? trades = null,
        FriendRegistry? friends = null, PartyRegistry? parties = null, MentorRegistry? mentors = null)
    {
        return new GuildInviteService(ZoneTestKit.CreateRegistry(), new GuildInviteRegistry(),
            duels ?? new DuelRegistry(), trades ?? new TradeRegistry(), friends ?? new FriendRegistry(),
            parties ?? new PartyRegistry(), mentors ?? new MentorRegistry());
    }

    [Fact]
    public void Ask_NeitherSideBusy_Sends()
    {
        var (zone, asker, target) = MakeAskerAndTarget();
        var service = CreateService();

        var result = service.Ask(zone, asker, target.Name);

        Assert.Equal(GuildInviteAskResultKind.Sent, result);
    }

    [Fact]
    public void Ask_AskerHasOpenPersonalShop_ReturnsAskerBusy_WithoutResolvingTarget()
    {
        var (zone, asker, _) = MakeAskerAndTarget();
        asker.PshopOpen = true;
        var service = CreateService();

        // Even an unresolvable target name must still short-circuit to AskerBusy -- legacy checks the asker's
        // own CheckCommunityWork()/stun-dead state before ever resolving the target.
        var result = service.Ask(zone, asker, "NoSuchAvatar");

        Assert.Equal(GuildInviteAskResultKind.AskerBusy, result);
    }

    [Fact]
    public void Ask_AskerStunned_ReturnsAskerBusy()
    {
        var (zone, asker, target) = MakeAskerAndTarget();
        asker.IsStunned = true;
        var service = CreateService();

        var result = service.Ask(zone, asker, target.Name);

        Assert.Equal(GuildInviteAskResultKind.AskerBusy, result);
    }

    [Fact]
    public void Ask_AskerDead_ReturnsAskerBusy()
    {
        var (zone, asker, target) = MakeAskerAndTarget();
        asker.IsDead = true;
        var service = CreateService();

        var result = service.Ask(zone, asker, target.Name);

        Assert.Equal(GuildInviteAskResultKind.AskerBusy, result);
    }

    [Fact]
    public void Ask_AskerNegotiatingADuel_ReturnsAskerBusy()
    {
        var (zone, asker, target) = MakeAskerAndTarget();
        var duels = new DuelRegistry();
        duels.TryAsk(asker.CharacterId, 999, false);
        var service = CreateService(duels);

        var result = service.Ask(zone, asker, target.Name);

        Assert.Equal(GuildInviteAskResultKind.AskerBusy, result);
    }

    [Fact]
    public void Ask_TargetNegotiatingATrade_ReturnsTargetBusy()
    {
        var (zone, asker, target) = MakeAskerAndTarget();
        var trades = new TradeRegistry();
        trades.TryAsk(target.CharacterId, 999);
        var service = CreateService(trades: trades);

        var result = service.Ask(zone, asker, target.Name);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

    [Fact]
    public void Ask_TargetHasPendingFriendRequest_ReturnsTargetBusy()
    {
        var (zone, asker, target) = MakeAskerAndTarget();
        var friends = new FriendRegistry();
        friends.TryAsk(999, target.CharacterId);
        var service = CreateService(friends: friends);

        var result = service.Ask(zone, asker, target.Name);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

    [Fact]
    public void Ask_TargetHasPendingPartyInvite_ReturnsTargetBusy()
    {
        var (zone, asker, target) = MakeAskerAndTarget();
        var parties = new PartyRegistry();
        parties.TryInvite(999, 1, target.Tribe, target.CharacterId, 1, target.Tribe);
        var service = CreateService(parties: parties);

        var result = service.Ask(zone, asker, target.Name);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

    [Fact]
    public void Ask_TargetNegotiatingAsAMentor_ReturnsTargetBusy()
    {
        var (zone, asker, target) = MakeAskerAndTarget();
        var mentors = new MentorRegistry();
        mentors.TryAsk(target.CharacterId, 999, false, false);
        var service = CreateService(mentors: mentors);

        var result = service.Ask(zone, asker, target.Name);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

    [Fact]
    public void Ask_TargetStunned_ReturnsTargetBusy()
    {
        var (zone, asker, target) = MakeAskerAndTarget();
        target.IsStunned = true;
        var service = CreateService();

        var result = service.Ask(zone, asker, target.Name);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }

    [Fact]
    public void Ask_TargetDead_ReturnsTargetBusy()
    {
        var (zone, asker, target) = MakeAskerAndTarget();
        target.IsDead = true;
        var service = CreateService();

        var result = service.Ask(zone, asker, target.Name);

        Assert.Equal(GuildInviteAskResultKind.TargetBusy, result);
    }
}
