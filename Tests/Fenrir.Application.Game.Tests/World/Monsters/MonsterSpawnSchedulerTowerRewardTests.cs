using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the tower CP-for-PvM (<see cref="TowerCpForPvmMilestone" />) and XP (<see cref="TowerWarState" />
///     tribe bonus table) monster-kill consumption hooks wired into <see cref="MonsterSpawnScheduler.Simulate" />
///     and <see cref="Zone.GrantMonsterKillExperience" />.
/// </summary>
public class MonsterSpawnSchedulerTowerRewardTests
{
    private static WorldDataCache CacheWithOneMonster(int monsterId = 500, short realLevel = 1,
        int generalExperience = 1000)
    {
        var monster = WorldDataTestRows.Monster(monsterId) with
        {
            Life = 100,
            ItemLevel = realLevel,
            RealLevel = realLevel,
            GeneralExperience = generalExperience,
            SummonTime1 = 100,
            SummonTime2 = 100,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            RadiusInfo1 = 50,
            RadiusInfo2 = 1000,
            WalkSpeed = 10,
            RunSpeed = 50
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, monsterId) with
        {
            Number = 1,
            LocationX = 100,
            LocationY = 0,
            LocationZ = 100,
            Radius = 5
        };

        // MinimalRows()'s own Level(1) row only spans experience [0,100] -- any of this file's monster kills
        // (generalExperience defaults to 1000) would otherwise blow straight past every uncatalogued level
        // band and resolve to LevelProgressionCalculator.MaxLevel (145, the deliberate "no matching band"
        // fallback -- see that class's own ReturnLevel remarks), silently overwriting killer.Level out from
        // under every test in this file before TowerCpForPvmMilestone ever reads it. Widening level 1's own
        // band keeps the killer's level exactly what CreateZoneWithKiller asked for, for the whole test.
        var levels = new[] { WorldDataTestRows.Level(1) with { ExpRangeMax = 1_000_000 } };
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [monster], MonsterSpawnRegions = [region], Levels = levels
        };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static (Zone Zone, TowerWarState TowerWar) CreateZoneWithKiller(WorldDataCache cache,
        byte killerTribe = 0, short killerLevel = 1)
    {
        var towerWar = new TowerWarState();
        var scheduler = new MonsterSpawnScheduler(cache, towerWar: towerWar);
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache, towerWar: towerWar);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, 1, "Killer", tribe: killerTribe, level: killerLevel)));
        zone.Tick(SimulationClock.LegacyTick);

        return (zone, towerWar);
    }

    [Fact]
    public void MonsterKill_KillersTribeHasNoXpTower_GrantsOnlyTheBaseExperience()
    {
        var cache = CacheWithOneMonster();
        var (zone, _) = CreateZoneWithKiller(cache);
        Assert.Equal(1, zone.MonsterCount);

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(10, out var killer));
        var baseGain = killer!.Experience;
        Assert.True(baseGain > 0);
    }

    [Fact]
    public void MonsterKill_KillersTribeHasALevelFourXpTower_GrantsDoubleTheBaseExperience()
    {
        var cache = CacheWithOneMonster(generalExperience: 1000);

        // Baseline: no tower.
        var (baselineZone, _) = CreateZoneWithKiller(cache);
        baselineZone.TryDamageMonster(1, 10_000, 10, out _, out _);
        baselineZone.Tick(SimulationClock.LegacyTick);
        Assert.True(baselineZone.TryGetPlayer(10, out var baselineKiller));
        var baselineGain = baselineKiller!.Experience;

        // Tribe 0's tower index 0 (slot-local 0): level 4 (raw digit 8), type 3 (XP) -- +100% ratio.
        var (boostedZone, towerWar) = CreateZoneWithKiller(cache);
        towerWar.SetTowerState(0, 8 * 100 + 3, true);
        towerWar.RecomputeTribeBonuses();
        boostedZone.TryDamageMonster(1, 10_000, 10, out _, out _);
        boostedZone.Tick(SimulationClock.LegacyTick);
        Assert.True(boostedZone.TryGetPlayer(10, out var boostedKiller));
        var boostedGain = boostedKiller!.Experience;

        // rawGain doubles (raw monster XP * 1.0 ratio added on top), then the same rebirth divisor applies to
        // the combined total -- so the boosted gain is roughly double the baseline, not merely "greater than".
        Assert.True(boostedGain > baselineGain);
        Assert.Equal(baselineGain * 2, boostedGain);
    }

    [Fact]
    public void MonsterKill_OtherTribesXpTower_NeverAffectsAKillerFromADifferentTribe()
    {
        var cache = CacheWithOneMonster(generalExperience: 1000);
        var (zone, towerWar) = CreateZoneWithKiller(cache, 1);

        // Tribe 0's tower (index 0-2), not tribe 1's -- must not affect a tribe-1 killer.
        towerWar.SetTowerState(0, 8 * 100 + 3, true);
        towerWar.RecomputeTribeBonuses();

        var (baselineZone, _) = CreateZoneWithKiller(cache, 1);
        baselineZone.TryDamageMonster(1, 10_000, 10, out _, out _);
        baselineZone.Tick(SimulationClock.LegacyTick);
        Assert.True(baselineZone.TryGetPlayer(10, out var baselineKiller));

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetPlayer(10, out var killer));

        Assert.Equal(baselineKiller!.Experience, killer!.Experience);
    }

    [Fact]
    public void MonsterKill_LevelAppropriate_IncrementsTheCpMilestoneCounter_ButGrantsNoCpBeforeThe1000th()
    {
        var cache = CacheWithOneMonster(realLevel: 1);
        var (zone, _) = CreateZoneWithKiller(cache, killerLevel: 1);
        Assert.True(zone.TryGetPlayer(10, out var killer));
        killer!.TowerCpMilestoneCounter = 5;

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(6, killer.TowerCpMilestoneCounter);
        Assert.Equal(0, killer.ContributionPoints);
    }

    [Fact]
    public void MonsterKill_The1000thLevelAppropriateKill_GrantsBaseCpAndResetsTheCounter()
    {
        var cache = CacheWithOneMonster(realLevel: 1);
        var (zone, _) = CreateZoneWithKiller(cache, killerLevel: 1);
        Assert.True(zone.TryGetPlayer(10, out var killer));
        killer!.TowerCpMilestoneCounter = 999;

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, killer.TowerCpMilestoneCounter);
        Assert.Equal(1, killer.ContributionPoints); // base killcp, no tower CP-for-PvM bonus active
    }

    [Fact]
    public void MonsterKill_The1000thKill_WithAnActiveCpForPvmTower_AddsTheFlatBonusOnTopOfTheBase()
    {
        var cache = CacheWithOneMonster(realLevel: 1);
        var (zone, towerWar) = CreateZoneWithKiller(cache, killerLevel: 1);
        Assert.True(zone.TryGetPlayer(10, out var killer));
        killer!.TowerCpMilestoneCounter = 999;

        // Tribe 0's tower index 1 (slot-local 1): level 2 (raw digit 4), type 2 (CP) -- CP-for-PvM +2.
        towerWar.SetTowerState(1, 4 * 100 + 2, true);
        towerWar.RecomputeTribeBonuses();

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, killer.TowerCpMilestoneCounter);
        Assert.Equal(3, killer.ContributionPoints); // base 1 + tower bonus 2
    }

    [Fact]
    public void MonsterKill_TooLargeALevelGap_NeverCountsTowardTheMilestone()
    {
        // Killer far above the monster's level (fixedLevel - monsterRealLevel >= 10) -- too easy a kill to
        // count toward the milestone.
        var cache = CacheWithOneMonster(realLevel: 1);
        var (zone, _) = CreateZoneWithKiller(cache, killerLevel: 50);
        Assert.True(zone.TryGetPlayer(10, out var killer));
        killer!.TowerCpMilestoneCounter = 500;

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(500, killer.TowerCpMilestoneCounter); // unchanged -- gap far exceeds the <10 window
    }

    [Fact]
    public void MonsterKill_WithNoResolvableKiller_NeverThrows_AndTouchesNoCpState()
    {
        var cache = CacheWithOneMonster();
        var (zone, _) = CreateZoneWithKiller(cache);

        zone.TryDamageMonster(1, 10_000, 999, out _, out _); // unresolvable killer id

        zone.Tick(SimulationClock.LegacyTick); // must not throw

        Assert.True(zone.TryGetPlayer(10, out var killer));
        Assert.Equal(0, killer!.TowerCpMilestoneCounter);
        Assert.Equal(0, killer.ContributionPoints);
    }
}
