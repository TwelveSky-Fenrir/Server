using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterBossSpawnSystemTests
{
    private const int Base = MonsterBossSpawnMachine.DefaultBossSlotBase;

    private static WorldDataCache CacheWithBossMonster(int monsterId = 500)
    {
        var monster = WorldDataTestRows.Monster(monsterId) with
        {
            Life = 100,
            ItemLevel = 1,
            RealLevel = 1,
            GeneralExperience = 40,
            SummonTime1 = 2,
            SummonTime2 = 2,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            RadiusInfo1 = 50,
            RadiusInfo2 = 1000,
            WalkSpeed = 10,
            RunSpeed = 50
        };
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [monster],
            MonsterSpawnRegions = []
        };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static MonsterBossSummonCatalog CatalogFor(short mapId, params MonsterBossSummonCandidate[] candidates)
    {
        return new MonsterBossSummonCatalog(new Dictionary<short, ImmutableArray<MonsterBossSummonCandidate>>
        {
            [mapId] = [.. candidates]
        });
    }

    [Fact]
    public void EmptyCatalog_IsInert_NeverSpawnsAnyBoss()
    {
        var cache = CacheWithBossMonster();
        var system = new MonsterBossSpawnSystem(cache);
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [system], worldData: cache);

        for (var i = 0; i < 60; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void FirstReload_SpawnsFiveBosses_AtTheVerbatimCandidateCoordinate_InTheReservedSlotWindow()
    {
        var cache = CacheWithBossMonster();
        var candidate = new MonsterBossSummonCandidate(500, 100f, 5f, 200f, 10f, 1);
        var system = new MonsterBossSpawnSystem(cache, CatalogFor(1, candidate));
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [system], worldData: cache);

        for (var i = 0; i < 20; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(5, zone.MonsterCount);
        Assert.Equal(5, system.LiveBossCountFor(zone));

        Assert.True(zone.TryGetMonster(Base, out var first));
        Assert.Equal(500, first!.Template.MonsterId);
        Assert.Equal(100f, first.PosX);
        Assert.Equal(5f, first.PosY);
        Assert.Equal(200f, first.PosZ);

        Assert.True(zone.TryGetMonster(Base + 4, out _));
        Assert.False(zone.TryGetMonster(Base + 5, out _));
    }

    [Fact]
    public void BeforeTheTwentyTickBoundary_NoBossHasSpawnedYet()
    {
        var cache = CacheWithBossMonster();
        var candidate = new MonsterBossSummonCandidate(500, 100f, 5f, 200f, 10f, 1);
        var system = new MonsterBossSpawnSystem(cache, CatalogFor(1, candidate));
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [system], worldData: cache);

        for (var i = 0; i < 19; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void UnresolvableBossId_SpawnsNothing_AndNeverThrows()
    {
        var cache = CacheWithBossMonster(500);
        var candidate = new MonsterBossSummonCandidate(999, 100f, 5f, 200f, 10f, 1);
        var system = new MonsterBossSpawnSystem(cache, CatalogFor(1, candidate));
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [system], worldData: cache);

        for (var i = 0; i < 60; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void KilledBoss_FreesItsSlot_SoLiveBossCountDrops()
    {
        var cache = CacheWithBossMonster();
        var candidate = new MonsterBossSummonCandidate(500, 100f, 5f, 200f, 10f, 1);
        var system = new MonsterBossSpawnSystem(cache, CatalogFor(1, candidate));
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [system], worldData: cache);

        for (var i = 0; i < 20; i++)
            zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(5, system.LiveBossCountFor(zone));

        Assert.True(zone.TryDamageMonster(Base, 1_000_000, null, out var died, out _));
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(4, system.LiveBossCountFor(zone));
        Assert.False(zone.TryGetMonster(Base, out _));
    }

    [Fact]
    public void ReservedBossSlotWindow_IsDisjointFromRegularMonsterServerIndexRange()
    {
        Assert.True(Base >= 3400);
    }
}
