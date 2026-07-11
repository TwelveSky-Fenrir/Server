using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

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

        var (baselineZone, _) = CreateZoneWithKiller(cache);
        baselineZone.TryDamageMonster(1, 10_000, 10, out _, out _);
        baselineZone.Tick(SimulationClock.LegacyTick);
        Assert.True(baselineZone.TryGetPlayer(10, out var baselineKiller));
        var baselineGain = baselineKiller!.Experience;

        var (boostedZone, towerWar) = CreateZoneWithKiller(cache);
        towerWar.SetTowerState(0, 8 * 100 + 3, true);
        towerWar.RecomputeTribeBonuses();
        boostedZone.TryDamageMonster(1, 10_000, 10, out _, out _);
        boostedZone.Tick(SimulationClock.LegacyTick);
        Assert.True(boostedZone.TryGetPlayer(10, out var boostedKiller));
        var boostedGain = boostedKiller!.Experience;

        Assert.True(boostedGain > baselineGain);
        Assert.Equal(baselineGain * 2, boostedGain);
    }

    [Fact]
    public void MonsterKill_OtherTribesXpTower_NeverAffectsAKillerFromADifferentTribe()
    {
        var cache = CacheWithOneMonster(generalExperience: 1000);
        var (zone, towerWar) = CreateZoneWithKiller(cache, 1);

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
        Assert.Equal(1, killer.ContributionPoints);
    }

    [Fact]
    public void MonsterKill_The1000thKill_WithAnActiveCpForPvmTower_AddsTheFlatBonusOnTopOfTheBase()
    {
        var cache = CacheWithOneMonster(realLevel: 1);
        var (zone, towerWar) = CreateZoneWithKiller(cache, killerLevel: 1);
        Assert.True(zone.TryGetPlayer(10, out var killer));
        killer!.TowerCpMilestoneCounter = 999;

        towerWar.SetTowerState(1, 4 * 100 + 2, true);
        towerWar.RecomputeTribeBonuses();

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, killer.TowerCpMilestoneCounter);
        Assert.Equal(3, killer.ContributionPoints);
    }

    [Fact]
    public void MonsterKill_TooLargeALevelGap_NeverCountsTowardTheMilestone()
    {
        var cache = CacheWithOneMonster(realLevel: 1);
        var (zone, _) = CreateZoneWithKiller(cache, killerLevel: 50);
        Assert.True(zone.TryGetPlayer(10, out var killer));
        killer!.TowerCpMilestoneCounter = 500;

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(500, killer.TowerCpMilestoneCounter);
    }

    [Fact]
    public void MonsterKill_WithNoResolvableKiller_NeverThrows_AndTouchesNoCpState()
    {
        var cache = CacheWithOneMonster();
        var (zone, _) = CreateZoneWithKiller(cache);

        zone.TryDamageMonster(1, 10_000, 999, out _, out _);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(10, out var killer));
        Assert.Equal(0, killer!.TowerCpMilestoneCounter);
        Assert.Equal(0, killer.ContributionPoints);
    }
}
