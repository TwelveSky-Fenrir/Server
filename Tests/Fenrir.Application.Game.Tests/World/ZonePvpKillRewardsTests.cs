using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the PvP-kill reward pipeline layered on top of <c>Zone.ApplyCombatCommand</c>'s HP-death path
///     (<see cref="Zone" />'s <c>ApplyPvpKillRewards</c>, formerly <c>ApplyPvpKillMissionProgress</c>): CP-formula
///     grant, the FFA-335 CP override + hero-point branch, EXP grant, the combined-level gate, and the (now
///     per-zone-gated) daily-mission-progress counter. HP-death itself and the C05 cooldown gate are covered by
///     <c>ZoneAttackTests</c>/<c>ZonePvpKillMissionProgressTests</c> respectively.
/// </summary>
public class ZonePvpKillRewardsTests
{
    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    private static readonly EffectiveStats WeakDefender =
        new(1000, 1000, 0, 200, 100, 0, 0, 0, 0, 0, 0);

    private static AttackForProtocol MeleeRequest(int attackerId, int defenderId)
    {
        return new AttackForProtocol
        {
            Case = 2,
            ServerIndex1 = attackerId,
            UniqueNumber1 = unchecked((uint)attackerId),
            ServerIndex2 = defenderId,
            UniqueNumber2 = unchecked((uint)defenderId),
            SenderLocation = [100, 0, 100],
            AttackActionValue1 = 1,
            AttackActionValue2 = 0,
            AttackActionValue3 = 0,
            AttackActionValue4 = 0,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
    }

    private static Zone SetUpZone(short mapId, short attackerLevel = 42, short defenderLevel = 42)
    {
        var zone = ZoneTestKit.CreateZone(mapId, randomSource: new ScriptedRandomSource(0, 0));

        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(attackerSession, mapId, "Attacker", tribe: 0, level: attackerLevel)));

        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2,
            ZoneTestKit.EnterData(defenderSession, mapId, "Defender", tribe: 1, level: defenderLevel)));

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.Stats = StrongAttacker;
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Stats = WeakDefender;
        // Legal, already-acting pose -- see ZoneAttackTests' own TwoPlayerZone remarks for the full reasoning
        // (CombatResolver.ResolveEnemyTribeAttack's defenderActionSort gate).
        defender.ActionSort = 1;

        // This suite exercises the PvP-kill reward pipeline, not the attack sub-packet budget/replay guard
        // (that's AttackPacketBudgetTests' own job) -- a real client always sends a legal avatar-action packet
        // first to establish a non-zero ceiling, which this fixture skips. Uncapped here so a raw
        // CombatCommand posted straight after Enter isn't silently rejected by AttackPacketBudget.TryConsume.
        attacker.AttackSubPacketCeiling = int.MaxValue;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1)); // past the zone-entry protect window
        return zone;
    }

    private static void KillDefender(Zone zone)
    {
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1; // one hit will kill regardless of exact damage roll

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
    }

    [Fact]
    public void DefaultZoneKill_GrantsFormulaBasedContributionPoints()
    {
        var zone = SetUpZone(1);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(PvpKillContributionPointCalculator.ComputeBaseAmount(false, false),
            attacker!.ContributionPoints);
    }

    [Fact]
    public void DefaultZoneKill_GrantsExperience()
    {
        var zone = SetUpZone(1);
        Assert.True(zone.TryGetPlayer(1, out var attackerBefore));
        var experienceBefore = attackerBefore!.Experience;

        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(attacker!.Experience > experienceBefore);
    }

    [Fact]
    public void DefaultZoneKill_GrantsNoHeroPoints()
    {
        var zone = SetUpZone(1);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(0, attacker!.HeroRankPoints);
    }

    [Fact]
    public void FfaZoneKill_GrantsFlatOverrideContributionPoints_NotTheGenericFormula()
    {
        var zone = SetUpZone(PvpKillRewardZoneCatalog.FfaMapNumber);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(PvpKillContributionPointCalculator.FfaOverrideFlatAmount, attacker!.ContributionPoints);
    }

    [Fact]
    public void FfaZoneKill_GrantsFixedHeroPointAmount()
    {
        var zone = SetUpZone(PvpKillRewardZoneCatalog.FfaMapNumber);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(PvpKillRewardZoneCatalog.FfaHeroPointAmount, attacker!.HeroRankPoints);
    }

    [Fact]
    public void FfaZoneKill_GrantsNoExperience()
    {
        var zone = SetUpZone(PvpKillRewardZoneCatalog.FfaMapNumber);
        Assert.True(zone.TryGetPlayer(1, out var attackerBefore));
        var experienceBefore = attackerBefore!.Experience;

        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(experienceBefore, attacker!.Experience);
    }

    [Fact]
    public void FfaZoneKill_GrantsNoDailyMissionProgress()
    {
        var zone = SetUpZone(PvpKillRewardZoneCatalog.FfaMapNumber);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(0, attacker!.MissionKillOtherTribe);
    }

    [Fact]
    public void UnconditionalFullRewardZone_GrantsDailyMissionProgress()
    {
        var zone = SetUpZone(194);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(1, attacker!.MissionKillOtherTribe);
    }

    [Fact]
    public void CombinedLevelGapPast13_GrantsNothingAtAll_ButKillStillHappens()
    {
        var zone = SetUpZone(1, 60, 40); // gap = 20 > 13
        Assert.True(zone.TryGetPlayer(1, out var attackerBefore));
        var experienceBefore = attackerBefore!.Experience;

        KillDefender(zone); // still asserts IsDead internally

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(0, attacker!.ContributionPoints);
        Assert.Equal(0, attacker.HeroRankPoints);
        Assert.Equal(0, attacker.MissionKillOtherTribe);
        Assert.Equal(experienceBefore, attacker.Experience);
    }

    [Fact]
    public void CombinedLevelGapAtThirteen_StillGrantsRewards()
    {
        var zone = SetUpZone(1, 53, 40); // gap = 13, at the cutoff
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(PvpKillContributionPointCalculator.ComputeBaseAmount(false, false),
            attacker!.ContributionPoints);
    }
}
