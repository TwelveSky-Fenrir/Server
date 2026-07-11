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

public class TribeSymbolSpawnerTests
{
    private static readonly byte[] SpecialTypes = [11, 12, 13, 28, 14];

    private static WorldDataCache CacheWithAllFiveSymbols(int life = 100)
    {
        var monsters = Enumerable.Range(0, 5)
            .Select(i => WorldDataTestRows.Monster(601 + i) with { SpecialType = SpecialTypes[i], Life = life })
            .ToArray();

        var rows = WorldDataTestRows.MinimalRows() with { Monsters = monsters };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static TribeSymbolCatalog CatalogWith(byte symbolIndex, byte ownerState, short mapId, float x = 1f,
        float z = 1f)
    {
        var byOwner = ImmutableDictionary<byte, TribeSymbolPlacement>.Empty
            .Add(ownerState, new TribeSymbolPlacement(mapId, x, 0f, z));
        var bySymbol = ImmutableDictionary<byte, ImmutableDictionary<byte, TribeSymbolPlacement>>.Empty
            .Add(symbolIndex, byOwner);
        return new TribeSymbolCatalog(bySymbol);
    }

    private static WorldStateService CreateWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    private static Zone CreateZone(short mapId, WorldDataCache cache)
    {
        return ZoneTestKit.CreateZone(mapId, worldData: cache);
    }

    [Fact]
    public void NoMatchingTemplate_SkipsEverySymbol_NeverThrows()
    {
        var cache = WorldDataCacheBuilder.Build(WorldDataTestRows.MinimalRows()).Cache;
        var worldState = CreateWorldState();
        var spawner = new TribeSymbolSpawner(cache, TribeSymbolCatalog.Empty, worldState);
        var zone = CreateZone(5, cache);

        spawner.EvaluateNow(zone);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void OwnTribeStillHoldsItsSymbol_AndThisIsItsDesignatedMap_Spawns()
    {
        var cache = CacheWithAllFiveSymbols();
        var catalog = CatalogWith(0, 0, 5);
        var worldState = CreateWorldState();
        var spawner = new TribeSymbolSpawner(cache, catalog, worldState);
        var zone = CreateZone(5, cache);

        spawner.EvaluateNow(zone);

        Assert.Equal(1, zone.MonsterCount);
        Assert.Equal(601, zone.MonstersSnapshot.Single().Template.MonsterId);
    }

    [Fact]
    public void NotFoundAnywhere_AndThisIsNotTheDesignatedMap_DoesNothing()
    {
        var cache = CacheWithAllFiveSymbols();
        var catalog = CatalogWith(0, 0, 7);
        var worldState = CreateWorldState();
        var spawner = new TribeSymbolSpawner(cache, catalog, worldState);
        var zone = CreateZone(5, cache);

        spawner.EvaluateNow(zone);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void AlreadyPresentAndCorrectlyPlaced_IsIdempotent_NeverDuplicated()
    {
        var cache = CacheWithAllFiveSymbols();
        var catalog = CatalogWith(0, 0, 5);
        var worldState = CreateWorldState();
        var spawner = new TribeSymbolSpawner(cache, catalog, worldState);
        var zone = CreateZone(5, cache);

        spawner.EvaluateNow(zone);
        spawner.EvaluateNow(zone);
        spawner.EvaluateNow(zone);

        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void CapturedTribeSymbol_IsUnresolvable_AndIsSkippedRatherThanGuessed()
    {
        var cache = CacheWithAllFiveSymbols();
        var catalog = CatalogWith(0, 0, 5);
        var worldState = CreateWorldState();
        worldState.ResolveTribeSymbol(0, 1);
        var spawner = new TribeSymbolSpawner(cache, catalog, worldState);
        var zone = CreateZone(5, cache);

        spawner.EvaluateNow(zone);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void OwnershipMovedElsewhere_DespawnsTheLocalCopy_OnlyLocally()
    {
        var cache = CacheWithAllFiveSymbols();
        var worldState = CreateWorldState();
        var zone = CreateZone(5, cache);

        var spawnerHere = new TribeSymbolSpawner(cache, CatalogWith(0, 0, 5), worldState);
        spawnerHere.EvaluateNow(zone);
        Assert.Equal(1, zone.MonsterCount);

        var spawnerMoved = new TribeSymbolSpawner(cache, CatalogWith(0, 0, 7), worldState);
        spawnerMoved.EvaluateNow(zone);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void NeutralSymbol_Unclaimed_UsesTheUnclaimedSentinelPlacement()
    {
        var cache = CacheWithAllFiveSymbols();
        var catalog = CatalogWith(4, TribeSymbolCatalog.NeutralUnclaimedOwnerState, 5);
        var worldState = CreateWorldState();
        var spawner = new TribeSymbolSpawner(cache, catalog, worldState);
        var zone = CreateZone(5, cache);

        spawner.EvaluateNow(zone);

        Assert.Equal(1, zone.MonsterCount);
        Assert.Equal(605, zone.MonstersSnapshot.Single().Template.MonsterId);
    }

    [Fact]
    public void NeutralSymbol_CapturedByATribe_UsesThatTribesOwnerStatePlacement()
    {
        var cache = CacheWithAllFiveSymbols();
        var catalog = CatalogWith(4, 2, 9);
        var worldState = CreateWorldState();
        worldState.ResolveMonsterSymbol(2);
        var spawner = new TribeSymbolSpawner(cache, catalog, worldState);
        var zoneNine = CreateZone(9, cache);
        var zoneFive = CreateZone(5, cache);

        spawner.EvaluateNow(zoneNine);
        spawner.EvaluateNow(zoneFive);

        Assert.Equal(1, zoneNine.MonsterCount);
        Assert.Equal(0, zoneFive.MonsterCount);
    }

    [Fact]
    public void SmallestTribe_SpawningItsOwnSymbol_GetsDoubleHealth()
    {
        var cache = CacheWithAllFiveSymbols();
        var catalog = CatalogWith(0, 0, 5);
        var worldState = CreateWorldState();
        var spawner = new TribeSymbolSpawner(cache, catalog, worldState);
        var zone = CreateZone(5, cache);

        EnterPlayers(zone, [1, 2, 2, 2]);

        spawner.EvaluateNow(zone);

        Assert.Equal(1, zone.MonsterCount);
        Assert.Equal(200, zone.MonstersSnapshot.Single().MaxLife);
    }

    [Fact]
    public void NotTheSmallestTribe_SpawningItsOwnSymbol_GetsNormalHealth()
    {
        var cache = CacheWithAllFiveSymbols();
        var catalog = CatalogWith(0, 0, 5);
        var worldState = CreateWorldState();
        var spawner = new TribeSymbolSpawner(cache, catalog, worldState);
        var zone = CreateZone(5, cache);

        EnterPlayers(zone, [2, 1, 2, 2]);

        spawner.EvaluateNow(zone);

        Assert.Equal(1, zone.MonsterCount);
        Assert.Equal(100, zone.MonstersSnapshot.Single().MaxLife);
    }

    private static void EnterPlayers(Zone zone, int[] tribeCounts)
    {
        var characterId = 10;
        for (byte tribe = 0; tribe < tribeCounts.Length; tribe++)
        for (var i = 0; i < tribeCounts[tribe]; i++)
        {
            var (session, _) = ZoneTestKit.CreateSession(characterId);
            zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId, tribe: tribe)));
            characterId++;
        }

        zone.Tick(SimulationClock.LegacyTick);
    }
}
