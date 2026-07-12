using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using static Fenrir.Application.Game.Tests.GameData.WorldDataTestRows;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterBossSummonCatalogTests
{
    [Fact]
    public void BuildFrom_PopulatesCandidatesFromBossSpawnRegions_ForTheOwningZone()
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(500)],
            MonsterSpawnRegions =
            [
                BossSpawnRegion(1, 1, 500, locationX: 10, locationY: 20, locationZ: 30, radius: 5),
                BossSpawnRegion(2, 1, 500, locationX: 40, locationY: 20, locationZ: 30, radius: 5)
            ]
        };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        var catalog = MonsterBossSummonCatalog.BuildFrom(cache);

        var candidates = catalog.CandidatesFor(1);
        Assert.Equal(2, candidates.Length);
        Assert.Equal(500, candidates[0].MonsterId);
        Assert.Equal(10f, candidates[0].X);
        Assert.Equal(40f, candidates[1].X);
    }

    [Fact]
    public void BuildFrom_LeavesZonesWithOnlyFieldMonsterRegions_Inert()
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(500)],
            MonsterSpawnRegions = [SpawnRegion(1, 1, 500)]
        };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        var catalog = MonsterBossSummonCatalog.BuildFrom(cache);

        Assert.Empty(catalog.CandidatesFor(1));
    }

    [Fact]
    public void BuildFrom_AppliesTheBossOnlyEvenCountRule_DroppingATrailingOddRow()
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(500)],
            MonsterSpawnRegions =
            [
                BossSpawnRegion(1, 1, 500),
                BossSpawnRegion(2, 1, 500),
                BossSpawnRegion(3, 1, 500)
            ]
        };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        var catalog = MonsterBossSummonCatalog.BuildFrom(cache);

        Assert.Equal(2, catalog.CandidatesFor(1).Length);
    }

    [Fact]
    public void BuildFrom_LeavesZonesWithNoSpawnRegionsAtAll_OutOfTheCatalog()
    {
        var cache = WorldDataCacheBuilder.Build(MinimalRows()).Cache;

        var catalog = MonsterBossSummonCatalog.BuildFrom(cache);

        Assert.Empty(catalog.CandidatesFor(1));
    }
}
