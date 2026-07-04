using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone.GrantMonsterKillExperience" />'s flat party bonus: granted to every present (same zone,
///     not dead) party member, including the killer again on top of their own solo gain (<c>Server/ts25zone/S07_MyGame05.cpp</c>).
/// </summary>
public class ZonePartyExperienceTests
{
    private static (Zone Zone, int KillerId, int MemberId) SetUpTwoPresentPartyMembers()
    {
        var zone = ZoneTestKit.CreateZone(1);

        var (killerSession, _) = ZoneTestKit.CreateSession(1);
        var (memberSession, _) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(killerSession, 1, "Killer", level: 50)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(memberSession, 1, "Ally", level: 50)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        return (zone, 10, 20);
    }

    [Fact]
    public void GrantMonsterKillExperience_WithTwoPresentPartyMembers_GrantsBonusToBothIncludingKiller()
    {
        var (zone, killerId, memberId) = SetUpTwoPresentPartyMembers();

        zone.GrantMonsterKillExperience(killerId, 50, 1000,
            [killerId, memberId]);

        zone.TryGetPlayer(killerId, out var killer);
        zone.TryGetPlayer(memberId, out var member);

        // Killer: solo base gain (equal levels, gap=0 -> raw=1000, /3 below LV_M1 -> 333) + 10% party bonus (100).
        Assert.Equal(333 + 100, killer!.Experience);
        // Non-killing member: ONLY the flat bonus, no share of the base kill gain.
        Assert.Equal(100, member!.Experience);
    }

    [Fact]
    public void GrantMonsterKillExperience_SoloKiller_NoPartyIds_GrantsOnlyBaseGain()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Solo", level: 50)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.GrantMonsterKillExperience(10, 50, 1000);

        zone.TryGetPlayer(10, out var killer);
        Assert.Equal(333, killer!.Experience);
    }

    [Fact]
    public void GrantMonsterKillExperience_DeadPartyMember_IsExcludedFromPresentCount()
    {
        var (zone, killerId, memberId) = SetUpTwoPresentPartyMembers();
        zone.ApplyDeath(memberId);

        zone.GrantMonsterKillExperience(killerId, 50, 1000,
            [killerId, memberId]);

        zone.TryGetPlayer(killerId, out var killer);
        zone.TryGetPlayer(memberId, out var member);

        // Only 1 present member (the killer) -- below the 2-member threshold, so no bonus at all.
        Assert.Equal(333, killer!.Experience);
        Assert.Equal(0, member!.Experience);
    }

    [Fact]
    public void GrantMonsterKillExperience_PartyMemberInDifferentZone_IsNotGranted()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Killer", level: 50)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // characterId 20 is a party member per the roster, but never entered this zone
        zone.GrantMonsterKillExperience(10, 50, 1000,
            [10, 20]);

        zone.TryGetPlayer(10, out var killer);
        Assert.Equal(333, killer!.Experience); // no bonus -- only 1 present member found
    }
}
