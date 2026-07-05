using Fenrir.Application.Game.Forge;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Forge;

public class RerollResolverTests
{
    private static ItemRowDto Item(int itemId, byte type, byte sort, short level, byte martialLevel = 0,
        byte equipInfo1 = 1, byte checkExchange = 2, byte checkAvatarTrade = 0, byte checkHighItem = 0,
        byte checkLowItem = 0, byte checkImprove = 0, byte checkHighImprove = 0, byte checkSetItem = 0)
    {
        return WorldDataTestRows.Item(itemId) with
        {
            Type = type, Sort = sort, Level = level, MartialLevel = martialLevel, EquipInfo1 = equipInfo1,
            CheckExchange = checkExchange, CheckAvatarTrade = checkAvatarTrade, CheckHighItem = checkHighItem,
            CheckLowItem = checkLowItem, CheckImprove = checkImprove, CheckHighImprove = checkHighImprove,
            CheckSetItem = checkSetItem
        };
    }

    [Fact]
    public void TargetTypeNormal_IsRejected()
    {
        var target = Item(1, 0, 9, 100);
        var result = RerollResolver.Resolve(target, 0, [], new ScriptedRandomSource(0));

        Assert.Equal(RerollResolver.RerollOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void SortOutOfExchangeRange_IsRejected()
    {
        var target = Item(1, RerollResolver.RareItemType, 22, 100);
        var result = RerollResolver.Resolve(target, 0, [], new ScriptedRandomSource(0));

        Assert.Equal(RerollResolver.RerollOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void CheckExchangeNotTwo_IsRejected()
    {
        var target = Item(1, RerollResolver.RareItemType, 9, 100, checkExchange: 0);
        var result = RerollResolver.Resolve(target, 0, [], new ScriptedRandomSource(0));

        Assert.Equal(RerollResolver.RerollOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void EmptyCatalog_ReturnsNoCandidateWithComputedCost()
    {
        var target = Item(1, RerollResolver.RareItemType, 9, 45);
        var result = RerollResolver.Resolve(target, 0, [], new ScriptedRandomSource(0));

        Assert.Equal(RerollResolver.RerollOutcome.NoCandidate, result.Outcome);
        Assert.Equal(1_000_000, result.Cost);
    }

    [Fact]
    public void LowTier_MatchingCandidate_SelfInclusionAllowed()
    {
        // IARMOR = 9, tribe 0's weapon slots are ISWORD/IBLADE/IMARBLE -- force the roll onto category index 2
        // (IARMOR, the 3rd of 9 Rare categories) so a self-match (only candidate in the catalog) is picked.
        var target = Item(1, RerollResolver.RareItemType, 9, 55, equipInfo1: 3);
        var result = RerollResolver.Resolve(target, 0, [target], new ScriptedRandomSource(2, 0));

        Assert.Equal(RerollResolver.RerollOutcome.Success, result.Outcome);
        Assert.Equal(1, result.ResultItemId);
        Assert.Equal(1_200_000, result.Cost);
    }

    [Fact]
    public void HighTier_ExcludesSelf()
    {
        var target = Item(1, RerollResolver.RareItemType, 9, 145, 3, 3, checkHighItem: 1,
            checkLowItem: 1, checkImprove: 1, checkHighImprove: 1);
        var result = RerollResolver.Resolve(target, 0, [target], new ScriptedRandomSource(2, 0));

        Assert.Equal(RerollResolver.RerollOutcome.NoCandidate, result.Outcome);
    }

    [Fact]
    public void HighTier_DifferentItemSameEncodedTier_IsSelected()
    {
        var target = Item(1, RerollResolver.RareItemType, 9, 145, 3, 3, checkHighItem: 1,
            checkLowItem: 1, checkImprove: 1, checkHighImprove: 1);
        var candidate = Item(2, RerollResolver.RareItemType, 9, 145, 3, 3, checkHighItem: 1,
            checkLowItem: 1, checkImprove: 1, checkHighImprove: 1);
        var result = RerollResolver.Resolve(target, 0, [target, candidate], new ScriptedRandomSource(2, 0));

        Assert.Equal(RerollResolver.RerollOutcome.Success, result.Outcome);
        Assert.Equal(2, result.ResultItemId);
    }

    [Fact]
    public void TribeThree_WeaponCategorySlotsAreZero_YieldsNoCandidateWhenPicked()
    {
        // Rare category list is [Amulet,Cape,Armor,Glove,Ring,Boots,w1,w2,w3] -- roll index 6 (w1) which is 0
        // for tribe 3 (no switch case in the legacy), so ReturnForExchangeItem's own iSort<1 guard rejects it.
        var target = Item(1, RerollResolver.RareItemType, 9, 100);
        var candidate = Item(2, RerollResolver.RareItemType, 9, 100, equipInfo1: 1);
        var result = RerollResolver.Resolve(target, 3, [target, candidate], new ScriptedRandomSource(6, 0));

        Assert.Equal(RerollResolver.RerollOutcome.NoCandidate, result.Outcome);
    }

    [Theory]
    [InlineData(45, 1_000_000)]
    [InlineData(144, 4_400_000)]
    public void RareCost_MatchesLegacyTable(short level, int expectedCost)
    {
        var target = Item(1, RerollResolver.RareItemType, 9, level);
        var result = RerollResolver.Resolve(target, 0, [], new ScriptedRandomSource(0));

        Assert.Equal(expectedCost, result.Cost);
    }

    [Fact]
    public void RareCost_Level145Martial12_MatchesLegacyTable()
    {
        var target = Item(1, RerollResolver.RareItemType, 9, 145, 12);
        var result = RerollResolver.Resolve(target, 0, [], new ScriptedRandomSource(0));

        Assert.Equal(7_000_000, result.Cost);
    }

    [Theory]
    [InlineData(100, 4_000_000)]
    [InlineData(142, 8_800_000)]
    public void EliteCost_MatchesLegacyTable(short level, int expectedCost)
    {
        var target = Item(1, RerollResolver.EliteItemType, 9, level);
        var result = RerollResolver.Resolve(target, 0, [], new ScriptedRandomSource(0));

        Assert.Equal(expectedCost, result.Cost);
    }

    [Fact]
    public void EliteCost_Level145Martial12_MatchesLegacyTable()
    {
        var target = Item(1, RerollResolver.EliteItemType, 9, 145, 12);
        var result = RerollResolver.Resolve(target, 0, [], new ScriptedRandomSource(0));

        Assert.Equal(14_000_000, result.Cost);
    }
}
