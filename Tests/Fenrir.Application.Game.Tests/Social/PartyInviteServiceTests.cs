using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Social;

/// <summary>
///     Covers <see cref="PartyInviteService.Invite" />'s response-code-order: the inviter's own busy/pose
///     state (already partied and not the leader, or still negotiating another invite) must be checked
///     before the target avatar is resolved by name (Server/ts25zone/S04_MyWork02.cpp:9088-9101).
/// </summary>
public class PartyInviteServiceTests
{
    private static (PartyInviteService Service, ZoneRegistry Zones, PartyRegistry Parties) CreateService(short mapId)
    {
        var parties = new PartyRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([mapId]);
        return (new PartyInviteService(parties, new CapturingLogger<PartyInviteService>()), zones, parties);
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
    public void Invite_InviterAlreadyPartiedAndNotLeader_AndTargetNameDoesNotExist_ReturnsInviterMustDisconnect()
    {
        var (service, zones, parties) = CreateService(1);
        Enter(zones, 1, 1, "Leader");
        var inviter = Enter(zones, 1, 2, "Member");

        Assert.Equal(PartyInviteOutcome.Sent, parties.TryInvite(1, 10, 1, 2, 10, 1));
        Assert.True(parties.TryAnswer(2, true, out _, out _)); // 2 joins 1's party as a non-leader member

        var result = service.Invite(zones[1], inviter, "NoSuchAvatar");

        Assert.Equal(PartyInviteResultKind.InviterMustDisconnect, result.Kind);
    }

    [Fact]
    public void Invite_InviterBusy_AndTargetNameDoesNotExist_ReturnsInviterBusy_NotTargetNotFound()
    {
        var (service, zones, parties) = CreateService(1);
        var inviter = Enter(zones, 1, 1, "Inviter");
        Enter(zones, 1, 2, "PendingTarget");

        Assert.Equal(PartyInviteOutcome.Sent, parties.TryInvite(1, 10, 1, 2, 10, 1)); // still pending

        var result = service.Invite(zones[1], inviter, "NoSuchAvatar");

        Assert.Equal(PartyInviteResultKind.InviterBusy, result.Kind);
    }

    [Fact]
    public void Invite_InviterNotBusy_TargetNameDoesNotExist_ReturnsTargetNotFound()
    {
        var (service, zones, _) = CreateService(1);
        var inviter = Enter(zones, 1, 1, "Inviter");

        var result = service.Invite(zones[1], inviter, "NoSuchAvatar");

        Assert.Equal(PartyInviteResultKind.TargetNotFound, result.Kind);
    }

    /// <summary>
    ///     Covers the party-invite level-gate combined-level extension: the gap check must compare
    ///     <see cref="PlayerRuntimeState.CombinedLevel" /> (aLevel1+aLevel2), not aLevel1 alone.
    /// </summary>
    [Fact]
    public void Invite_CombinedLevelGapWithinTolerance_Sends_EvenThoughOrdinaryLevelGapAlone_ExceedsIt()
    {
        var (service, zones, _) = CreateService(1);
        var inviter = Enter(zones, 1, 1, "Inviter", level: 30);
        var target = Enter(zones, 1, 2, "Target", level: 10);
        // Ordinary-level-only gap would be 20 (exceeds the 9 tolerance, would wrongly disconnect); the
        // combined-level gap is 30-25=5, within tolerance.
        target.Level2 = 15;

        var result = service.Invite(zones[1], inviter, "Target");

        Assert.Equal(PartyInviteResultKind.Sent, result.Kind);
    }

    [Fact]
    public void Invite_CombinedLevelGapExceedsTolerance_MustDisconnect_EvenThoughOrdinaryLevelGapAlone_DoesNot()
    {
        var (service, zones, _) = CreateService(1);
        var inviter = Enter(zones, 1, 1, "Inviter", level: 10);
        var target = Enter(zones, 1, 2, "Target", level: 9);
        // Ordinary-level-only gap would be 1 (within tolerance, would wrongly accept); the combined-level gap
        // is 24-10=14, exceeding the 9 tolerance.
        target.Level2 = 15;

        var result = service.Invite(zones[1], inviter, "Target");

        Assert.Equal(PartyInviteResultKind.InviterMustDisconnect, result.Kind);
    }
}
