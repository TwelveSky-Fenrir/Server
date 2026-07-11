using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class BossEventDropResolverTests
{
    private static readonly BossDropCatalog Catalog = BossDropCatalog.Default;

    private static WorldDataCache EmptyItems()
    {
        return ZoneTestKit.EmptyWorldData();
    }

    private static WorldDataCache CacheContaining(int itemId)
    {
        var rows = WorldDataTestRows.MinimalRows() with { Items = [WorldDataTestRows.Item(itemId)] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    [Fact]
    public void Santa731_DropsItem536_AndFallsThroughToGenericTiers()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.SantaMonsterId, 0, new Random(1),
            EmptyItems(), Catalog);

        Assert.Equal([new DroppedItem(536, 1)], outcome.Items);
        Assert.False(outcome.SkipGenericTiers);
    }

    [Fact]
    public void NineItemEventBoss1404_DropsTheNineItemList_AndGrants50Cp_AndFallsThrough()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.NineItemEventBossMonsterId, 0,
            new Random(1), EmptyItems(), Catalog);

        Assert.Equal(Catalog.NineItemEventList, outcome.Items);
        Assert.Equal(50, outcome.ContributionPointsGranted);
        Assert.False(outcome.SkipGenericTiers);
    }

    [Fact]
    public void HolyUnicorn576_DropsPersonalList_AndPublic20xLabyrinthKey_AndFallsThrough()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.HolyUnicornMonsterId, 0, new Random(1),
            EmptyItems(), Catalog);

        Assert.Equal(Catalog.HolyUnicornPersonalList, outcome.Items);
        Assert.Equal([new DroppedItem(1048, 20)], outcome.PublicItems);
        Assert.False(outcome.SkipGenericTiers);
    }

    [Fact]
    public void ThreeItemEventBoss756_DropsTheThreeItemList_AndFallsThrough()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.ThreeItemEventBossMonsterId, 0,
            new Random(1), EmptyItems(), Catalog);

        Assert.Equal(Catalog.ThreeItemEventList, outcome.Items);
        Assert.False(outcome.SkipGenericTiers);
    }

    [Theory]
    [InlineData(564)]
    [InlineData(568)]
    public void CustomTimedBoss_DropsItsOwnList_AndAbortsGenericTiers(int monsterId)
    {
        var outcome = BossEventDropResolver.Resolve(monsterId, 0, new Random(1), EmptyItems(), Catalog);

        Assert.Equal(Catalog.CustomTimedBossLists[monsterId], outcome.Items);
        Assert.True(outcome.SkipGenericTiers);
    }

    [Fact]
    public void EliteBoss1407_DropsGuaranteedList_AbortsGenericTiers_AndAnnouncesDefeat()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.EliteBossMonsterId, 0, new Random(1),
            EmptyItems(), Catalog);

        Assert.Equal(Catalog.EliteBossGuaranteedList, outcome.Items);
        Assert.True(outcome.SkipGenericTiers);
        Assert.True(outcome.AnnounceEliteBossDefeat);
    }

    [Fact]
    public void DemonLord287_OnTheTenthKill_PicksOneItemFromThePool_AndAborts()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.DemonLordMonsterId, 10,
            new SequenceRandom(0), EmptyItems(), Catalog);

        Assert.Equal([new DroppedItem(8109, 1)], outcome.Items);
        Assert.True(outcome.SkipGenericTiers);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(0)]
    public void DemonLord287_OnANonTenthKill_DropsNothing_ButStillAborts(int tally)
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.DemonLordMonsterId, tally,
            new SequenceRandom(0), EmptyItems(), Catalog);

        Assert.Empty(outcome.Items);
        Assert.True(outcome.SkipGenericTiers);
    }

    [Fact]
    public void FifteenMinuteBoss1408_MidTier_PicksFromTheNineItemPool_AndDoublesItem1449()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.FifteenMinuteBossMonsterId, 0,
            new SequenceRandom(200, 2), EmptyItems(), Catalog);

        Assert.Equal([new DroppedItem(1449, 2)], outcome.Items);
        Assert.False(outcome.SkipGenericTiers);
    }

    [Fact]
    public void FifteenMinuteBoss1408_HighTier_PicksFromTheThreeItemPool()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.FifteenMinuteBossMonsterId, 0,
            new SequenceRandom(500, 0), EmptyItems(), Catalog);

        Assert.Equal([new DroppedItem(695, 1)], outcome.Items);
    }

    [Fact]
    public void FifteenMinuteBoss1408_LowTier_DropsASingleRandomTier2Animal()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.FifteenMinuteBossMonsterId, 0,
            new SequenceRandom(10, 0, 0), EmptyItems(), Catalog);

        Assert.Equal([new DroppedItem(1304, 1)], outcome.Items);
    }

    [Fact]
    public void FifteenMinuteBoss1408_LowMidTier_CanPickTheRandomTier1Animal()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.FifteenMinuteBossMonsterId, 0,
            new SequenceRandom(50, 0, 2), EmptyItems(), Catalog);

        Assert.Equal([new DroppedItem(1301, 1)], outcome.Items);
    }

    [Fact]
    public void FifteenMinuteBoss1408_LowMidTier_CanPickAFixedId()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.FifteenMinuteBossMonsterId, 0,
            new SequenceRandom(50, 0, 0), EmptyItems(), Catalog);

        Assert.Equal([new DroppedItem(1178, 1)], outcome.Items);
    }

    [Fact]
    public void VirginGhost746_GrantsWarAndBloodPoints_PicksFromSharedPool_AndAborts()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.VirginGhostMonsterId, 0,
            new SequenceRandom(0, 0), EmptyItems(), Catalog);

        Assert.Equal([new DroppedItem(1023, 1)], outcome.Items);
        Assert.Equal(0, outcome.ContributionPointsGranted);
        Assert.Equal(2, outcome.WarPointsGranted);
        Assert.Equal(2, outcome.BloodPointsGranted);
        Assert.True(outcome.SkipGenericTiers);
    }

    [Fact]
    public void VirginGhost746_WhenRareItemExists_AndRollPasses_AlsoDropsThe30PercentBonus()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.VirginGhostMonsterId, 0,
            new SequenceRandom(0, 0, 0, 0), CacheContaining(93500), Catalog);

        Assert.Equal([new DroppedItem(93500, 1), new DroppedItem(1023, 1)], outcome.Items);
        Assert.True(outcome.SkipGenericTiers);
    }

    [Fact]
    public void SharedRandomPool9001_GrantsCpWarAndBloodPoints_PicksFromSharedPool_AndAborts()
    {
        var outcome = BossEventDropResolver.Resolve(BossEventDropResolver.SharedRandomPoolMonsterId, 0,
            new SequenceRandom(0, 0), EmptyItems(), Catalog);

        Assert.Equal([new DroppedItem(1023, 1)], outcome.Items);
        Assert.Equal(5, outcome.ContributionPointsGranted);
        Assert.Equal(3, outcome.WarPointsGranted);
        Assert.Equal(6, outcome.BloodPointsGranted);
        Assert.True(outcome.SkipGenericTiers);
    }

    [Fact]
    public void UnmatchedMonsterId_ResolvesToNone()
    {
        var outcome = BossEventDropResolver.Resolve(99999, 0, new Random(1), EmptyItems(), Catalog);

        Assert.Equal(BossDropOutcome.None, outcome);
        Assert.Empty(outcome.Items);
        Assert.False(outcome.SkipGenericTiers);
    }

        private sealed class SequenceRandom(params int[] raw) : Random
    {
        private int _index;

        public override int Next(int maxValue)
        {
            return maxValue <= 0 ? 0 : Mod(Take(), maxValue);
        }

        public override int Next(int minValue, int maxValue)
        {
            return maxValue <= minValue ? minValue : minValue + Mod(Take(), maxValue - minValue);
        }

        private int Take()
        {
            return raw[_index++];
        }

        private static int Mod(int value, int modulus)
        {
            return ((value % modulus) + modulus) % modulus;
        }
    }
}
