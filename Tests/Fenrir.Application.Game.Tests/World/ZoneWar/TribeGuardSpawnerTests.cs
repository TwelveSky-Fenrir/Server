using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeGuardSpawnerTests
{
    private const byte MainType = 5;
    private const byte SpecialType = 7;

    private static WorldDataCache CacheWithGuardTemplate(int monsterId = 900, int life = 100,
        int summonTimeSeconds = 2)
    {
        var monster = WorldDataTestRows.Monster(monsterId) with
        {
            Type = MainType,
            SpecialType = SpecialType,
            Life = life,
            SummonTime1 = summonTimeSeconds,
            SummonTime2 = summonTimeSeconds
        };

        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static GuardPostDefinition Post(short mapId, byte tribeId, int slotCount = 5,
        byte? requiresTribeSymbolOwnedBy = null)
    {
        var slots = Enumerable.Range(0, slotCount)
            .Select(i => new GuardSlotCoordinate(i * 10f, 0f, i * 10f, i))
            .ToImmutableArray();
        return new GuardPostDefinition(mapId, tribeId, MainType, SpecialType, slots, requiresTribeSymbolOwnedBy);
    }

    private static WorldStateService CreateWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    private static Zone CreateZone(short mapId, WorldDataCache cache, TribeGuardSpawner spawner)
    {
        return ZoneTestKit.CreateZone(mapId, simulationSystems: [spawner], worldData: cache);
    }

    [Fact]
    public void EligibleMap_BootTick_ForceSpawnsEveryConfiguredSlot()
    {
        var cache = CacheWithGuardTemplate();
        var catalog = new GuardPostCatalog([Post(2, 0, 3)], []);
        var spawner = new TribeGuardSpawner(cache, catalog);
        var zone = CreateZone(2, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(3, zone.MonsterCount);
    }

    [Fact]
    public void MapNotInTheHardcodedEligibleSet_NeverSpawnsAnything()
    {
        var cache = CacheWithGuardTemplate();
        var catalog = new GuardPostCatalog([Post(99, 0, 3)], []);
        var spawner = new TribeGuardSpawner(cache, catalog);
        var zone = CreateZone(99, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void NoMatchingMonsterTemplate_SkipsTheWholePost()
    {
        var cache = WorldDataCacheBuilder.Build(WorldDataTestRows.MinimalRows()).Cache;
        var catalog = new GuardPostCatalog([Post(2, 0, 3)], []);
        var spawner = new TribeGuardSpawner(cache, catalog);
        var zone = CreateZone(2, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void FourthTribePostsDisabled_SkipsOnlyTribeFour_NotOtherTribes()
    {
        var cache = CacheWithGuardTemplate();
        var catalog = new GuardPostCatalog([Post(2, 0, 2), Post(2, 3, 2)], []);
        var options = new TribeGuardOptions { FourthTribeGuardPostsEnabled = false };
        var spawner = new TribeGuardSpawner(cache, catalog, options: options);
        var zone = CreateZone(2, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(2, zone.MonsterCount);
    }

    [Fact]
    public void ConditionalFifthPost_SkippedWhole_WhileItsTribeHasLostItsOwnSymbol()
    {
        var cache = CacheWithGuardTemplate();
        var catalog = new GuardPostCatalog([Post(2, 1, 2, 1)], []);
        var worldState = CreateWorldState();
        worldState.ResolveTribeSymbol(1, 2);
        var spawner = new TribeGuardSpawner(cache, catalog, worldState);
        var zone = CreateZone(2, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void ConditionalFifthPost_Spawns_WhileItsTribeStillOwnsItsOwnSymbol()
    {
        var cache = CacheWithGuardTemplate();
        var catalog = new GuardPostCatalog([Post(2, 1, 2, 1)], []);
        var worldState = CreateWorldState();
        var spawner = new TribeGuardSpawner(cache, catalog, worldState);
        var zone = CreateZone(2, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(2, zone.MonsterCount);
    }

    [Fact]
    public void ForcedResummon_ReplacesTheLiveGuardWithAFreshOne()
    {
        var cache = CacheWithGuardTemplate();
        var catalog = new GuardPostCatalog([Post(2, 0, 1)], []);
        var spawner = new TribeGuardSpawner(cache, catalog);
        var zone = CreateZone(2, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);
        var before = zone.MonstersSnapshot.Single().UniqueNumber;

        spawner.ForceOrdinaryResummon(zone);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(1, zone.MonsterCount);
        Assert.NotEqual(before, zone.MonstersSnapshot.Single().UniqueNumber);
    }

    [Fact]
    public void KilledGuard_DoesNotRespawnBeforeItsTimerElapses_ThenRespawnsAfterEnoughEvaluations()
    {
        var cache = CacheWithGuardTemplate(summonTimeSeconds: 1);
        var catalog = new GuardPostCatalog([Post(2, 0, 1)], []);
        var spawner = new TribeGuardSpawner(cache, catalog);
        var zone = CreateZone(2, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        var serverIndex = zone.MonstersSnapshot.Single().ServerIndex;
        zone.TryDamageMonster(serverIndex, 10_000, null, out var died, out _);
        Assert.True(died);
        Assert.Equal(0, zone.MonsterCount);

        for (var i = 0; i < 19; i++)
            zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(0, zone.MonsterCount);

        for (var i = 0; i < 20; i++)
            zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void Zone038WinnerPool_NoOpUntilAWinnerIsRecorded()
    {
        var cache = CacheWithGuardTemplate();
        var catalog = new GuardPostCatalog([], [Post(TribeGuardSpawner.Zone038MapId, 0, 2)]);
        var worldState = CreateWorldState();
        var spawner = new TribeGuardSpawner(cache, catalog, worldState);
        var zone = CreateZone(TribeGuardSpawner.Zone038MapId, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void Zone038WinnerPool_SpawnsOnlyTheWinningTribesPost()
    {
        var cache = CacheWithGuardTemplate();
        var catalog = new GuardPostCatalog([],
            [Post(TribeGuardSpawner.Zone038MapId, 0, 2), Post(TribeGuardSpawner.Zone038MapId, 1, 2)]);
        var worldState = CreateWorldState();
        worldState.SetZone038Winner(1);
        var spawner = new TribeGuardSpawner(cache, catalog, worldState);
        var zone = CreateZone(TribeGuardSpawner.Zone038MapId, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(2, zone.MonsterCount);
    }

    [Fact]
    public void ForceZone038WinnerResummon_SpawnsOnNextTick_WithoutWaitingForBoot()
    {
        var cache = CacheWithGuardTemplate();
        var catalog = new GuardPostCatalog([], [Post(TribeGuardSpawner.Zone038MapId, 1, 2)]);
        var worldState = CreateWorldState();
        var spawner = new TribeGuardSpawner(cache, catalog, worldState);
        var zone = CreateZone(TribeGuardSpawner.Zone038MapId, cache, spawner);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(0, zone.MonsterCount);

        worldState.SetZone038Winner(1);
        spawner.ForceZone038WinnerResummon(zone);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(2, zone.MonsterCount);
    }
}
