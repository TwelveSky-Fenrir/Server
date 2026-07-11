using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ZoneRegularWarBossSummonTests
{
    private static WorldDataCache CacheWithBoss561()
    {
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [WorldDataTestRows.Monster(RegularWarBossSummonCatalog.BossMonsterId)]
        };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    [Fact]
    public void SummonRegularWarBoss_SpawnsExactlyThreeCopies_AtTheCatalogFixedPosition()
    {
        var zone = ZoneTestKit.CreateZone(295, worldData: CacheWithBoss561());

        zone.Post(ZoneCommand.SummonRegularWarBoss());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(RegularWarBossSummonCatalog.SummonCount, zone.MonsterCount);
        foreach (var monster in zone.MonstersSnapshot)
        {
            Assert.Equal(RegularWarBossSummonCatalog.BossMonsterId, monster.Template.MonsterId);
            Assert.Equal(RegularWarBossSummonCatalog.SummonX, monster.PosX);
            Assert.Equal(RegularWarBossSummonCatalog.SummonY, monster.PosY);
            Assert.Equal(RegularWarBossSummonCatalog.SummonZ, monster.PosZ);
        }
    }

    [Fact]
    public void NoSummonCommandPosted_SpawnsNothing()
    {
        var zone = ZoneTestKit.CreateZone(295, worldData: CacheWithBoss561());

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void MonsterCatalogMissingBoss561_IsANoOp_NeverThrows()
    {
        var zone = ZoneTestKit.CreateZone(295);

        zone.Post(ZoneCommand.SummonRegularWarBoss());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void RepeatedSummonCommand_SkipsNoExistenceCheck_EachCallAddsItsOwnThreeCopies()
    {
        var zone = ZoneTestKit.CreateZone(295, worldData: CacheWithBoss561());

        zone.Post(ZoneCommand.SummonRegularWarBoss());
        zone.Post(ZoneCommand.SummonRegularWarBoss());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(2 * RegularWarBossSummonCatalog.SummonCount, zone.MonsterCount);
    }

    [Fact]
    public void PoolExhausted_DropsOnlyTheOverflowCopies_NeverThrows()
    {
        var zone = ZoneTestKit.CreateZone(295, worldData: CacheWithBoss561());

        for (var i = 0; i < 34; i++)
            zone.Post(ZoneCommand.SummonRegularWarBoss());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(100, zone.MonsterCount);
    }
}
