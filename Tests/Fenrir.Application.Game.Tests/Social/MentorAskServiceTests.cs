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
///     Covers <see cref="MentorAskService.Ask" />'s response-code-order: the master's own still-negotiating
///     busy state must be checked before the target student is resolved by name
///     (Server/ts25zone/S04_MyWork02.cpp:9311-9324).
/// </summary>
public class MentorAskServiceTests
{
    private const short MasterLevel = 120;

    private static (MentorAskService Service, ZoneRegistry Zones, MentorRegistry Mentors) CreateService(short mapId)
    {
        var mentors = new MentorRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([mapId]);
        return (new MentorAskService(mentors, new DuelRegistry(), new TradeRegistry(), new FriendRegistry(),
            new PartyRegistry(), new GuildInviteRegistry(), new CapturingLogger<MentorAskService>()), zones,
            mentors);
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
    public void Ask_MasterBusy_AndTargetNameDoesNotExist_ReturnsAskerBusy_NotTargetNotFound()
    {
        var (service, zones, mentors) = CreateService(1);
        var master = Enter(zones, 1, 1, "Master");
        Enter(zones, 1, 2, "PendingStudent", level: 1);

        Assert.Equal(MentorAskOutcome.Sent, mentors.TryAsk(1, 2, false, false)); // still pending, never answered

        var result = service.Ask(zones[1], master, "NoSuchAvatar");

        Assert.Equal(MentorAskResultKind.AskerBusy, result.Kind);
    }

    [Fact]
    public void Ask_MasterNotBusy_TargetNameDoesNotExist_ReturnsTargetNotFound()
    {
        var (service, zones, _) = CreateService(1);
        var master = Enter(zones, 1, 1, "Master");

        var result = service.Ask(zones[1], master, "NoSuchAvatar");

        Assert.Equal(MentorAskResultKind.TargetNotFound, result.Kind);
    }

    [Fact]
    public void Ask_MasterLevelTooLow_ReturnsAskerMustDisconnect_BeforeBusyOrTargetChecks()
    {
        var (service, zones, mentors) = CreateService(1);
        var master = Enter(zones, 1, 1, "Master", level: 50);
        Enter(zones, 1, 2, "PendingStudent", level: 1);
        Assert.Equal(MentorAskOutcome.Sent, mentors.TryAsk(1, 2, false, false)); // also busy, must not matter

        var result = service.Ask(zones[1], master, "NoSuchAvatar");

        Assert.Equal(MentorAskResultKind.AskerMustDisconnect, result.Kind);
    }
}
