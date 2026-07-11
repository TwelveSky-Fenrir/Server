using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterSpawnSchedulerBossRespawnTests
{
    private static WorldDataCache CacheWithOneRegion(int monsterId, int regionId, int summonTimeSeconds)
    {
        var monster = WorldDataTestRows.Monster(monsterId) with
        {
            Life = 100,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = summonTimeSeconds,
            SummonTime2 = summonTimeSeconds
        };
        var region = WorldDataTestRows.SpawnRegion(regionId, 1, monsterId) with
        {
            Number = 1,
            LocationX = 0,
            LocationY = 0,
            LocationZ = 0,
            Radius = 0
        };

        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static MonsterBossRespawnTracker CreateTracker(FakeMonsterBossRespawnTimerRepository? repository = null)
    {
        var tracker = new MonsterBossRespawnTracker(NullLogger<MonsterBossRespawnTracker>.Instance);
        tracker.InitializeAsync(repository ?? new FakeMonsterBossRespawnTimerRepository(), CancellationToken.None)
            .GetAwaiter().GetResult();
        return tracker;
    }

    [Fact]
    public void Monster746_RespawnsAfterItsFixed240SecondOverride_NotTheCatalogedSummonTime()
    {
        var cache = CacheWithOneRegion(746, 1, 9999);
        var scheduler = new MonsterSpawnScheduler(cache);
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        zone.TryDamageMonster(1, 10_000, null, out var died, out _);
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick);

        for (var i = 0; i < 500; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void KillingAPersistedBossMonster_ArmsTheTracker_WithTheRolledDeadline()
    {
        var cache = CacheWithOneRegion(564, 77, 100);
        var repository = new FakeMonsterBossRespawnTimerRepository();
        var tracker = CreateTracker(repository);
        var scheduler = new MonsterSpawnScheduler(cache, bossRespawnTracker: tracker);
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.False(tracker.TryGetNextSpawnUtc(77, out _));

        var beforeKill = DateTime.UtcNow;
        zone.TryDamageMonster(1, 10_000, null, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(tracker.TryGetNextSpawnUtc(77, out var deadline));
        var expected = beforeKill.AddSeconds(100);
        Assert.True(Math.Abs((deadline - expected).TotalSeconds) < 2,
            $"expected ~{expected:o}, got {deadline:o}");
    }

    [Fact]
    public void Restart_WithAStillPendingPersistedDeadline_DoesNotPopTheSlot_UntilItElapses()
    {
        const int regionId = 9;
        var cache = CacheWithOneRegion(565, regionId, 5);
        var repository = new FakeMonsterBossRespawnTimerRepository
        {
            Rows = { [regionId] = DateTime.UtcNow.AddSeconds(3) }
        };
        var tracker = CreateTracker(repository);
        var scheduler = new MonsterSpawnScheduler(cache, bossRespawnTracker: tracker);
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(0, zone.MonsterCount);

        for (var i = 0; i < 30; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void Restart_WithAnAlreadyDuePersistedDeadline_PopsOnTheFirstTick()
    {
        const int regionId = 11;
        var cache = CacheWithOneRegion(566, regionId, 5);
        var repository = new FakeMonsterBossRespawnTimerRepository
        {
            Rows = { [regionId] = DateTime.UtcNow.AddSeconds(-30) }
        };
        var tracker = CreateTracker(repository);
        var scheduler = new MonsterSpawnScheduler(cache, bossRespawnTracker: tracker);
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void OrdinaryMonsterInThePersistedIdRange_WithNoTrackerWired_BehavesLikeAnyOtherMonster()
    {
        var cache = CacheWithOneRegion(567, 1, 2);
        var scheduler = new MonsterSpawnScheduler(cache);
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        zone.TryDamageMonster(1, 10_000, null, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        for (var i = 0; i < 40; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(1, zone.MonsterCount);
    }
}
