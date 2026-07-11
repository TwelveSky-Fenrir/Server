using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <c>Zone.HandleSummonRegularWarBoss</c> -- the A4-missing-bosses <c>ZoneCommandKind.SummonRegularWarBoss</c>
///     handler that closes the previously-ignored <c>bossMonstersShouldSpawn</c> flag. The posting side
///     (<c>RegularWarRewardGrantSink</c>) is covered by <see cref="RegularWarRewardGrantSinkTests" />; this file
///     only exercises the Zone-side summon mutation in isolation, same split precedent as
///     <c>ZoneRegularWarRewardGrantTests</c> covers the sibling reward-grant hook.
/// </summary>
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
        // Regression guard for the exact bug this cluster closes: before this handler existed,
        // bossMonstersShouldSpawn was silently ignored end-to-end. A plain tick with no
        // ZoneCommand.SummonRegularWarBoss posted must never spawn anything on its own.
        var zone = ZoneTestKit.CreateZone(295, worldData: CacheWithBoss561());

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void MonsterCatalogMissingBoss561_IsANoOp_NeverThrows()
    {
        var zone = ZoneTestKit.CreateZone(295); // EmptyWorldData -- no monster 561 defined

        zone.Post(ZoneCommand.SummonRegularWarBoss());
        zone.Tick(TimeSpan.FromMilliseconds(50)); // must not throw

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void RepeatedSummonCommand_SkipsNoExistenceCheck_EachCallAddsItsOwnThreeCopies()
    {
        // RegularWarBossSummonCatalog.SkipExistenceCheck: unlike Zone200's boss (756), a second summon batch
        // is never deduplicated against copies an earlier batch already placed -- it relies entirely on the
        // (not-yet-modeled, see Zone.RegularWarBossSummon.cs's own remarks) map-wide clear to avoid stacking.
        // Two commands in the same tick therefore leave 6 monsters, not 3.
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

        // 34 summon batches * 3 = 102 attempted placements against a 100-slot reserved range: the last 2
        // placements silently find no free slot and are dropped, same failure mode as every sibling summon
        // primitive (SpawnGmSummonedMonster/SummonPersonalBoss) when their own reserved range is exhausted.
        for (var i = 0; i < 34; i++)
            zone.Post(ZoneCommand.SummonRegularWarBoss());
        zone.Tick(TimeSpan.FromMilliseconds(50)); // must not throw

        Assert.Equal(100, zone.MonsterCount);
    }
}
