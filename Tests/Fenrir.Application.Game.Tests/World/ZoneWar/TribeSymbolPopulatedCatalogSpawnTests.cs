using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeSymbolPopulatedCatalogSpawnTests
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

    private static WorldStateService CreateWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    private static Zone Zone(short mapId, WorldDataCache cache)
    {
        return ZoneTestKit.CreateZone(mapId, worldData: cache);
    }

    [Fact]
    public void Tribe0OwnSymbol_SpawnsAtExactHomeCoordinate_OnMap2()
    {
        var cache = CacheWithAllFiveSymbols();
        var spawner = new TribeSymbolSpawner(cache, TribeSymbolPlacementCatalog.Default, CreateWorldState());
        var zone = Zone(2, cache);

        spawner.EvaluateNow(zone);

        var symbol = Assert.Single(zone.MonstersSnapshot);
        Assert.Equal(601, symbol.Template.MonsterId);
        Assert.Equal(-1810f, symbol.PosX);
        Assert.Equal(-1f, symbol.PosY);
        Assert.Equal(3155f, symbol.PosZ);
    }

    [Fact]
    public void Tribe0OwnSymbol_DoesNotSpawnOnAForeignMap()
    {
        var cache = CacheWithAllFiveSymbols();
        var spawner = new TribeSymbolSpawner(cache, TribeSymbolPlacementCatalog.Default, CreateWorldState());

        var zone = Zone(5, cache);
        spawner.EvaluateNow(zone);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void NeutralMonsterSymbol_Unclaimed_SpawnsAtNeutralHomeZone74()
    {
        var cache = CacheWithAllFiveSymbols();
        var spawner = new TribeSymbolSpawner(cache, TribeSymbolPlacementCatalog.Default, CreateWorldState());
        var zone = Zone(74, cache);

        spawner.EvaluateNow(zone);

        var symbol = Assert.Single(zone.MonstersSnapshot);
        Assert.Equal(605, symbol.Template.MonsterId);
        Assert.Equal(-2f, symbol.PosX);
        Assert.Equal(0f, symbol.PosY);
        Assert.Equal(2626f, symbol.PosZ);
    }

    [Fact]
    public void NeutralMonsterSymbol_CapturedByTribe2_RelocatesToZone14()
    {
        var cache = CacheWithAllFiveSymbols();
        var worldState = CreateWorldState();
        worldState.ResolveMonsterSymbol(2);
        var spawner = new TribeSymbolSpawner(cache, TribeSymbolPlacementCatalog.Default, worldState);

        var captorZone = Zone(14, cache);
        var neutralZone = Zone(74, cache);
        spawner.EvaluateNow(captorZone);
        spawner.EvaluateNow(neutralZone);

        var symbol = Assert.Single(captorZone.MonstersSnapshot);
        Assert.Equal(605, symbol.Template.MonsterId);
        Assert.Equal(7174f, symbol.PosX);
        Assert.Equal(336f, symbol.PosY);
        Assert.Equal(6191f, symbol.PosZ);

        Assert.Equal(0, neutralZone.MonsterCount);
    }

    [Fact]
    public void CapturedAway_DespawnsTheLocalCopy_OnItsFormerHostZone()
    {
        var cache = CacheWithAllFiveSymbols();
        var worldState = CreateWorldState();
        var spawner = new TribeSymbolSpawner(cache, TribeSymbolPlacementCatalog.Default, worldState);
        var zone74 = Zone(74, cache);

        spawner.EvaluateNow(zone74);
        Assert.Equal(1, zone74.MonsterCount);

        worldState.ResolveMonsterSymbol(1);
        spawner.EvaluateNow(zone74);

        Assert.Equal(0, zone74.MonsterCount);
    }
}
