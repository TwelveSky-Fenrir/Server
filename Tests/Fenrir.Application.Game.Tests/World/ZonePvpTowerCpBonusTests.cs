using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the tower CP-for-PvP consumption hook layered onto <c>Zone.ApplyPvpKillMissionProgress</c> (see
///     that method's own remarks) -- <see cref="ZonePvpKillMissionProgressTests" /> already covers the
///     pre-existing <see cref="PlayerRuntimeState.MissionKillOtherTribe" />/<see cref="KillCooldownTracker" />
///     wiring this hook is layered on top of; this file only asserts the tower CP bonus itself.
/// </summary>
/// <remarks>
///     Map id 49 is a "default"-branch zone (<see cref="PvpKillRewardZoneCatalog.Resolve" />), so every
///     qualifying kill here also grants the ever-present base PvP-kill CP-formula amount
///     (<see cref="PvpKillContributionPointCalculator.ComputeBaseAmount" />) alongside whatever tower bonus
///     applies -- both terms fold additively into the same legacy <c>tCPAddNum1</c> accumulator (see
///     <c>Zone.ApplyTowerCpForPvpBonus</c>'s own remarks), exactly as
///     <c>ZonePvpKillRewardsTests.DefaultZoneKill_GrantsFormulaBasedContributionPoints</c> already verifies
///     for a tower-less default zone. Every expected value below is therefore
///     <see cref="BaseKillCp" /> plus the tower bonus under test, not the tower bonus alone.
/// </remarks>
public class ZonePvpTowerCpBonusTests
{
    /// <summary>
    ///     The ever-present base PvP-kill CP grant (see class remarks) every assertion here adds on top of. B9:
    ///     the base amount now composes the game-wide cross-tribe add value (config 3, doubled to 6 by the
    ///     always-active rebirth build macro -- see <see cref="PvpKillContributionPointBonuses.ComputeGameWideAddValue" />)
    ///     instead of the old flat <see cref="PvpKillContributionPointCalculator.BasePerKillAmount" /> placeholder.
    ///     <see cref="PvpKillContributionPointBonuses.ComputeConditionalBonuses" /> is omitted here since map 49
    ///     is none of the three server ids (38/160/minority-capital 1-6-11-140) it gates on, so it always
    ///     contributes 0 regardless of tribe/level for every test in this file.
    /// </summary>
    private static readonly int BaseKillCp = PvpKillContributionPointCalculator.ComputeBaseAmount(false, false,
        basePerKillAmount: PvpKillContributionPointBonuses.ComputeGameWideAddValue(3));

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

    /// <remarks>
    ///     Map id 49 (not 1): still PvP-enabled (<see cref="ZonePvpZoneCatalog.AllowsEnemyTribeAttack" />) but
    ///     outside every territorial revive-eligibility block, so <see cref="DeathGateTickSystem" /> grants
    ///     revive-eligibility unconditionally past the 10-tick mark regardless of either combatant's tribe.
    /// </remarks>
    private static (Zone Zone, TowerWarState TowerWar) SetUpZone(int attackerId, params int[] defenderIds)
    {
        var towerWar = new TowerWarState();
        var worldState = ZoneTestKit.CreateWorldState();
        var zone = ZoneTestKit.CreateZone(49, randomSource: new ScriptedRandomSource(0, 0), towerWar: towerWar,
            worldState: worldState, simulationSystems: [new DeathGateTickSystem(worldState)]);

        var (attackerSession, _) = ZoneTestKit.CreateSession(attackerId);
        zone.Post(ZoneCommand.Enter(attackerId,
            ZoneTestKit.EnterData(attackerSession, 49, "Attacker", tribe: 0)));

        foreach (var defenderId in defenderIds)
        {
            var (defenderSession, _) = ZoneTestKit.CreateSession(defenderId);
            zone.Post(ZoneCommand.Enter(defenderId,
                ZoneTestKit.EnterData(defenderSession, 49, $"Defender{defenderId}", tribe: 1)));
        }

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(attackerId, out var attacker));
        attacker!.Stats = StrongAttacker;
        foreach (var defenderId in defenderIds)
        {
            Assert.True(zone.TryGetPlayer(defenderId, out var defender));
            defender!.Stats = WeakDefender;
            // Legal, already-acting pose -- see ZoneAttackTests' own TwoPlayerZone remarks for the full
            // reasoning (CombatResolver.ResolveEnemyTribeAttack's defenderActionSort gate).
            defender.ActionSort = 1;
        }

        // This suite exercises the tower CP-for-PvP bonus, not the attack sub-packet budget/replay guard
        // (that's AttackPacketBudgetTests' own job) -- a real client always sends a legal avatar-action packet
        // first to establish a non-zero ceiling, which this fixture skips. Uncapped here so a raw
        // CombatCommand posted straight after Enter isn't silently rejected by AttackPacketBudget.TryConsume.
        attacker.AttackSubPacketCeiling = int.MaxValue;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1)); // past the zone-entry protect window
        return (zone, towerWar);
    }

    [Fact]
    public void PvpKill_AttackersTribeHasNoCpForPvpTower_GrantsOnlyTheBaseKillCp()
    {
        var (zone, _) = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
        Assert.Equal(BaseKillCp, attacker!.ContributionPoints); // no tower bonus -- just the base kill CP
    }

    [Fact]
    public void PvpKill_AttackersTribeHasALevelThreeCpTower_GrantsTheFlatPvpBonus()
    {
        var (zone, towerWar) = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1;

        // Tribe 0's tower index 1 (slot-local 1): level 3 (raw digit 6), type 2 (CP) -- CP-for-PvP +1.
        towerWar.SetTowerState(1, 6 * 100 + 2, true);
        towerWar.RecomputeTribeBonuses();

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
        Assert.Equal(BaseKillCp + 1, attacker!.ContributionPoints);
    }

    [Fact]
    public void PvpKill_AttackersTribeHasALevelFourCpTower_GrantsTheFlatPvpBonusOfTwo()
    {
        var (zone, towerWar) = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1;

        towerWar.SetTowerState(1, 8 * 100 + 2, true); // level 4 CP tower
        towerWar.RecomputeTribeBonuses();

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(BaseKillCp + 2, attacker!.ContributionPoints);
    }

    [Fact]
    public void PvpKill_DefendersTribesCpTower_NeverAffectsTheAttackersContributionPoints()
    {
        var (zone, towerWar) = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1;

        // Tribe 1's tower (index 3, defender's own tribe), not tribe 0's (attacker's) -- must not apply.
        towerWar.SetTowerState(3, 8 * 100 + 2, true);
        towerWar.RecomputeTribeBonuses();

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Defender's tribe's tower contributes nothing -- only the ever-present base kill CP applies.
        Assert.Equal(BaseKillCp, attacker!.ContributionPoints);
    }

    [Fact]
    public void PvpKill_RepeatKillOfSameVictim_WithinTheC05Cooldown_DoesNotDoubleGrantTheTowerBonusEither()
    {
        var (zone, towerWar) = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        towerWar.SetTowerState(1, 6 * 100 + 2, true); // CP-for-PvP +1
        towerWar.RecomputeTribeBonuses();
        defender!.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(BaseKillCp + 1, attacker!.ContributionPoints);

        zone.Tick(SimulationClock.ReviveEligibilityDelay + TimeSpan.FromSeconds(1)); // revive-eligibility grant
        Assert.False(defender.IsDead);
        defender.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
        Assert.Equal(BaseKillCp + 1, attacker.ContributionPoints); // still unchanged -- gated by the same C05 cooldown
    }

    [Fact]
    public void NonLethalHit_GrantsNoContributionPoints_EvenWithATowerActive()
    {
        var (zone, towerWar) = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        towerWar.SetTowerState(1, 8 * 100 + 2, true);
        towerWar.RecomputeTribeBonuses();

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, attacker!.ContributionPoints);
    }
}
