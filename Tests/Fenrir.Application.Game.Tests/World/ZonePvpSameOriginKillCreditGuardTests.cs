using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers D2 hook 2/3 end-to-end: <c>PlayerRuntimeState.SourceIp</c> plumbed through
///     <c>EnterWorldService</c>/<c>ZoneTransfer.CreateEnterData</c>/<c>Zone.HandleEnter</c>, and
///     <c>Zone.ApplyPvpKillRewards</c>'s same-origin guard (<c>PvpKillCreditGuard.IsSameOrigin</c>)
///     that denies PvP-kill reward credit -- never the kill itself -- when killer and victim share a captured
///     source IP. Reuses <c>ZonePvpKillRewardsTests</c>' own <c>SetUpZone</c>/<c>KillDefender</c> shape so the
///     "same origin" case is a minimal, additive delta over that suite's already-covered default-zone-kill
///     reward path, not a parallel reimplementation of it.
/// </summary>
public class ZonePvpSameOriginKillCreditGuardTests
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

    private static Zone SetUpZone(short mapId, string? attackerSourceIp, string? defenderSourceIp)
    {
        var zone = ZoneTestKit.CreateZone(mapId, randomSource: new ScriptedRandomSource(0, 0));

        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(attackerSession, mapId, "Attacker", tribe: 0, sourceIp: attackerSourceIp)));

        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2,
            ZoneTestKit.EnterData(defenderSession, mapId, "Defender", tribe: 1, sourceIp: defenderSourceIp)));

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.Stats = StrongAttacker;
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Stats = WeakDefender;
        defender.ActionSort = 1; // legal, already-acting pose -- see ZoneAttackTests' own TwoPlayerZone remarks
        attacker.AttackSubPacketCeiling = int.MaxValue; // see ZonePvpKillRewardsTests' own remarks

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
    public void SameSourceIp_KillStillHappens_ButNoRewardCreditIsGranted()
    {
        var zone = SetUpZone(1, "203.0.113.7", "203.0.113.7");
        Assert.True(zone.TryGetPlayer(1, out var attackerBefore));
        var experienceBefore = attackerBefore!.Experience;

        KillDefender(zone); // still asserts IsDead internally -- the guard only withholds reward credit

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(0, attacker!.ContributionPoints);
        Assert.Equal(0, attacker.HeroRankPoints);
        Assert.Equal(0, attacker.MissionKillOtherTribe);
        Assert.Equal(experienceBefore, attacker.Experience);
    }

    [Fact]
    public void DifferentSourceIp_GrantsTheOrdinaryFormulaBasedContributionPoints()
    {
        var zone = SetUpZone(1, "203.0.113.7", "198.51.100.9");
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        // Same CP-formula composition as ZonePvpKillRewardsTests.DefaultZoneKill_GrantsFormulaBasedContributionPoints
        // -- proves the guard does not withhold credit for two genuinely distinct hosts.
        var expectedCp = PvpKillContributionPointCalculator.ComputeBaseAmount(false, false,
                basePerKillAmount: PvpKillContributionPointBonuses.ComputeGameWideAddValue(3))
            + PvpKillContributionPointBonuses.ComputeConditionalBonuses(1, 0, addedCpTribe: -1,
                attackerBaseLevel: 42, symbolBattleActive: false);
        Assert.Equal(expectedCp, attacker!.ContributionPoints);
    }

    /// <summary>
    ///     Regression guard for <see cref="SessionSourceIp.AreSameHost" />'s own "fail open" contract:
    ///     an absent source IP on both sides (the default for every test fixture across this codebase that
    ///     doesn't explicitly seed one, including every pre-existing <c>ZonePvpKillRewardsTests</c> case) must
    ///     never be treated as a same-origin match -- a null-vs-null pair proves nothing about shared origin.
    ///     If this regressed, activating D2 hooks 2/3 would have silently zeroed out PvP-kill rewards for every
    ///     test (and, more importantly, every real player) whose source IP was never captured for any reason.
    /// </summary>
    [Fact]
    public void BothSourceIpsNull_DoesNotFalselyBlockReward()
    {
        var zone = SetUpZone(1, null, null);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(attacker!.ContributionPoints > 0);
    }

    [Fact]
    public void OneSourceIpNull_DoesNotFalselyBlockReward()
    {
        var zone = SetUpZone(1, "203.0.113.7", null);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(attacker!.ContributionPoints > 0);
    }

    /// <summary>
    ///     D2 hook 3 layers additively on top of the pre-existing level-gap cap
    ///     (<c>Zone.CombinedLevelGapCap</c>) -- this proves the two guards are independent, not that one
    ///     subsumes the other: a level gap past the cap still withholds every reward on its own (already
    ///     covered by <c>ZonePvpKillRewardsTests.CombinedLevelGapPast13_GrantsNothingAtAll_ButKillStillHappens</c>),
    ///     and this same-origin guard produces the identical all-zero outcome through its own, earlier check
    ///     even when the level gap alone would have allowed the reward.
    /// </summary>
    [Fact]
    public void SameSourceIp_WithinLevelGap_StillGrantsNothing()
    {
        var zone = SetUpZone(1, "203.0.113.7", "203.0.113.7");
        // Re-affirm this isn't a level-gap artifact: both casters share SetUpZone's default level (42), well
        // inside CombinedLevelGapCap (13).
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        Assert.Equal(attacker!.Level, defender!.Level);

        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attackerAfter));
        Assert.Equal(0, attackerAfter!.ContributionPoints);
        Assert.Equal(0, attackerAfter.HeroRankPoints);
    }
}
