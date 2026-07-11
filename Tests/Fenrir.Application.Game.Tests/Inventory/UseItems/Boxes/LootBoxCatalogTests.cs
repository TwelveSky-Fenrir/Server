using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems.Boxes;

/// <summary>
///     Guards the fully-recoverable loot-box data in <see cref="LootBoxCatalog" />: which boxes are populated,
///     their exact reward-table shapes, and the bulk-open / notice sets. The "only fully-recoverable ids are
///     wired" rule is itself asserted -- a future edit that adds a box must add it here deliberately.
/// </summary>
public class LootBoxCatalogTests
{
    private static readonly LootBoxCatalog Catalog = LootBoxCatalog.Default;

    [Fact]
    public void RegisteredBoxIds_AreExactlyTheFullyRecoverableBoxes()
    {
        // 76543/1378/1379/1236/8005/8108/720 (workstreams C10-remaining-boxes / C10-remaining-box-pools) are
        // deliberately NOT here: each has a fully- or mostly-recovered reward table but does not fit
        // BoxRewardSpec's Uniform/Weighted/RareBandThenPools shapes -- their reward pool (or one band of it) is
        // keyed by the opening character's own previous-tribe code, which BoxRewardSpec.RollRewardId has no
        // parameter to receive. See LootBoxCatalog's own class remarks ("Deliberately NOT registered here").
        int[] expected = [601, 602, 635, 2249, 7105, 8112, 8113, 76542, 1240, 8111, 8114, 8115];
        Assert.Equal(expected.OrderBy(x => x), Catalog.RegisteredBoxIds.OrderBy(x => x));
    }

    [Fact]
    public void Box1240_IsUniform_OverTheFiveIdPool_NoRental()
    {
        var spec = Catalog.TryGetSpec(1240);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.Uniform, spec!.Kind);
        Assert.Equal(0, spec.RentalDays);
        int[] expectedIds = [506, 508, 509, 578, 579];
        Assert.Equal(expectedIds, spec.UniformIds.ToArray());
    }

    [Fact]
    public void Box8111_IsRareBandThenPools_WithEmptyRareBandsAndFivePools()
    {
        var spec = Catalog.TryGetSpec(8111);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec!.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Empty(spec.RareBands);
        Assert.Equal(5, spec.Pools.Length);
        Assert.Equal(149, spec.Pools[^1].ThresholdCeilingInclusive);
    }

    [Fact]
    public void Box8114_IsRareBandThenPools_WithEmptyRareBandsAndFourPools()
    {
        var spec = Catalog.TryGetSpec(8114);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec!.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Empty(spec.RareBands);
        Assert.Equal(4, spec.Pools.Length);
        Assert.Equal(499, spec.Pools[^1].ThresholdCeilingInclusive);
    }

    [Fact]
    public void Box8115_IsRareBandThenPools_WithEmptyRareBandsAndSixPools()
    {
        var spec = Catalog.TryGetSpec(8115);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec!.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Empty(spec.RareBands);
        Assert.Equal(6, spec.Pools.Length);
        Assert.Equal(149, spec.Pools[^1].ThresholdCeilingInclusive);
    }

    [Fact]
    public void Box601_IsRareBandThenPools_With635RareBandAndFivePools()
    {
        var spec = Catalog.TryGetSpec(601);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec!.Kind);
        Assert.Equal(0, spec.RentalDays);

        var rareBand = Assert.Single(spec.RareBands);
        Assert.Equal(50, rareBand.ThresholdPer10000);
        Assert.Equal(635, rareBand.RewardItemId);

        Assert.Equal(5, spec.Pools.Length);
        Assert.Equal(10, spec.Pools[0].ThresholdCeilingInclusive);
        Assert.Equal(200, spec.Pools[^1].ThresholdCeilingInclusive);
        Assert.Contains(1301, spec.Pools[1].Ids);
    }

    [Fact]
    public void Box602_IsRareBandThenPools_WithTwoRareBandsAndFivePools()
    {
        var spec = Catalog.TryGetSpec(602);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec!.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Equal(2, spec.RareBands.Length);
        Assert.Equal(5, spec.Pools.Length);
        Assert.Equal(199, spec.Pools[^1].ThresholdCeilingInclusive);
    }

    [Fact]
    public void Box635_IsUniform_OverEightTier3MountIds()
    {
        var spec = Catalog.TryGetSpec(635);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.Uniform, spec!.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Equal(8, spec.UniformIds.Length);
    }

    [Fact]
    public void Box2249_IsRareBandThenPools_WithOneRareBandAndTwoPools()
    {
        var spec = Catalog.TryGetSpec(2249);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec!.Kind);
        Assert.Equal(0, spec.RentalDays);
        var rareBand = Assert.Single(spec.RareBands);
        Assert.Equal(60, rareBand.ThresholdPer10000);
        Assert.Equal(1403, rareBand.RewardItemId);
        Assert.Equal(2, spec.Pools.Length);
        Assert.Equal(199, spec.Pools[^1].ThresholdCeilingInclusive);
    }

    [Fact]
    public void Box7105_IsWeighted_WithTheThreeCumulativeSpans()
    {
        var spec = Catalog.TryGetSpec(7105);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.Weighted, spec!.Kind);
        Assert.Equal(3, spec.WeightedRewards.Length);
        Assert.Equal(696, spec.WeightedRewards[0].ItemId);
        Assert.Equal(73, spec.WeightedRewards[0].Weight);
        Assert.Equal(2397, spec.WeightedRewards[2].ItemId);
        Assert.Equal(7, spec.WeightedRewards[2].Weight);
    }

    [Fact]
    public void Box8112_IsWeighted_WithTheSevenGradeBands()
    {
        var spec = Catalog.TryGetSpec(8112);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.Weighted, spec!.Kind);
        Assert.Equal(7, spec.WeightedRewards.Length);
        Assert.Equal(93500, spec.WeightedRewards[0].ItemId);
        Assert.Equal(350, spec.WeightedRewards[0].Weight);
        Assert.Equal(93506, spec.WeightedRewards[6].ItemId);
        Assert.Equal(30, spec.WeightedRewards[6].Weight);
    }

    [Fact]
    public void Box76542_IsUniform_WithThreeDayRental()
    {
        var spec = Catalog.TryGetSpec(76542);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.Uniform, spec!.Kind);
        Assert.Equal(3, spec.RentalDays);
        int[] expectedIds = [76534, 76535, 76536, 76537, 76538];
        Assert.Equal(expectedIds, spec.UniformIds.ToArray());
    }

    [Fact]
    public void Box8113_IsUniform_OverTheNineIdPool_NoRental()
    {
        var spec = Catalog.TryGetSpec(8113);

        Assert.NotNull(spec);
        Assert.Equal(BoxRewardKind.Uniform, spec!.Kind);
        Assert.Equal(0, spec.RentalDays);
        int[] expectedIds = [8101, 8102, 8106, 1103, 1126, 1166, 1222, 1237, 828];
        Assert.Equal(expectedIds, spec.UniformIds.ToArray());
    }

    [Fact]
    public void TryGetSpec_UnpopulatedBox_ReturnsNull()
    {
        Assert.Null(Catalog.TryGetSpec(999999));
    }

    [Fact]
    public void TryGetSpec_TribeKeyedBoxesNotYetIntegrated_ReturnNull()
    {
        // 76543/1378/1379/1236/8005/8108/720 have fully- or mostly-tested standalone reward tables
        // (CostumeChest76543RewardTable, WarlordChestRewardTable, HeavenlyJadeChest1236RewardTable,
        // WingLuckyBox8005RewardTable, LoyKrathongBox8108RewardTable, ChestBox720RewardTable) but are not
        // spliced into this catalog -- see RegisteredBoxIds_AreExactlyTheFullyRecoverableBoxes and
        // LootBoxCatalog's own class remarks.
        Assert.Null(Catalog.TryGetSpec(76543));
        Assert.Null(Catalog.TryGetSpec(1378));
        Assert.Null(Catalog.TryGetSpec(1379));
        Assert.Null(Catalog.TryGetSpec(1236));
        Assert.Null(Catalog.TryGetSpec(8005));
        Assert.Null(Catalog.TryGetSpec(8108));
        Assert.Null(Catalog.TryGetSpec(720));
    }

    [Fact]
    public void BulkOpenWhitelist_MatchesTheCitedIsBulkBoxNoStellarList()
    {
        foreach (var id in new[] { 512, 601, 602, 8112, 8113, 664, 720, 1236, 1240, 2249, 7105, 8108, 8111, 76543, 76544, 8005 })
            Assert.True(LootBoxCatalog.BulkOpenWhitelist.Contains(id), $"expected {id} in bulk whitelist");

        // 635 is explicitly commented out of the whitelist; 76542 is single-open only.
        Assert.DoesNotContain(635, LootBoxCatalog.BulkOpenWhitelist);
        Assert.DoesNotContain(76542, LootBoxCatalog.BulkOpenWhitelist);
    }

    [Fact]
    public void NoticeRewardWhitelist_HasThePetBoxSpecialTierIds_AndEliteOnlyBoxesAreTheThreeCitedIds()
    {
        Assert.Contains(1012, LootBoxCatalog.NoticeRewardWhitelist);
        Assert.Contains(1016, LootBoxCatalog.NoticeRewardWhitelist);
        Assert.Equal(2, LootBoxCatalog.NoticeRewardWhitelist.Count);

        Assert.Contains(1035, LootBoxCatalog.EliteOnlyNoticeBoxIds);
        Assert.Contains(1036, LootBoxCatalog.EliteOnlyNoticeBoxIds);
        Assert.Contains(1037, LootBoxCatalog.EliteOnlyNoticeBoxIds);
    }
}
