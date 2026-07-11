using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Tests.Progression;

public class LevelMilestoneBonusTests
{
    [Theory]
    [InlineData(45)]
    [InlineData(65)]
    [InlineData(85)]
    [InlineData(105)]
    [InlineData(114)] // LV_M2  -- recovered by the level-milestone-bonus-item-ids pass
    [InlineData(120)] // LV_M8
    [InlineData(126)] // LV_M14
    [InlineData(132)] // LV_M20
    [InlineData(138)] // LV_M26
    [InlineData(144)] // LV_M32
    [InlineData(145)]
    public void IsArmableMilestone_TrueForResolvableTiers(int level)
    {
        Assert.True(LevelMilestoneBonus.IsArmableMilestone(level));
    }

    [Theory]
    [InlineData(44)]
    [InlineData(46)]
    [InlineData(113)]
    [InlineData(0)]
    [InlineData(-1)]
    public void IsArmableMilestone_FalseForNonMilestoneLevels(int level)
    {
        Assert.False(LevelMilestoneBonus.IsArmableMilestone(level));
    }

    [Fact]
    public void DeferredMilestoneLevels_IsEmpty()
    {
        // All 11 named legacy milestone levels now have a known claim table (level-milestone-bonus-item-ids
        // recovered the last six, the M-tier levels) -- nothing is deferred any more.
        Assert.Empty(LevelMilestoneBonus.DeferredMilestoneLevels);
    }

    [Theory]
    [InlineData(44, 45, 45)] // exact single crossing
    [InlineData(44, 46, 45)] // overshoots one milestone
    [InlineData(44, 66, 65)] // crosses 45 AND 65 in one jump -> highest wins
    [InlineData(0, 145, 145)] // crosses every armable milestone -> the top one
    [InlineData(105, 145, 145)]
    [InlineData(44, 64, 45)] // 65 not yet reached
    [InlineData(105, 114, 114)] // crosses the first M-tier milestone (LV_M2)
    [InlineData(105, 144, 144)] // crosses all six M-tier milestones in one jump -> the top one (LV_M32)
    public void ResolveHighestMilestoneCrossed_ReturnsHighestArmableInRange(int previous, int next, int expected)
    {
        Assert.Equal(expected, LevelMilestoneBonus.ResolveHighestMilestoneCrossed(previous, next));
    }

    [Theory]
    [InlineData(45, 45)] // milestone equals previous level -> not "crossed" (strict lower bound)
    [InlineData(145, 145)]
    [InlineData(46, 64)] // no armable milestone strictly inside the range
    [InlineData(106, 113)] // next armable (114) not yet reached
    public void ResolveHighestMilestoneCrossed_ZeroWhenNoneCrossed(int previous, int next)
    {
        Assert.Equal(0, LevelMilestoneBonus.ResolveHighestMilestoneCrossed(previous, next));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(113)] // one below the first M-tier milestone -- still unrecognized
    public void TryResolveClaimDrops_FalseForUnrecognizedStoredLevel(int bonusItemLevel)
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(bonusItemLevel, 0, out var drops);

        Assert.False(resolved);
        Assert.True(drops.IsDefault);
    }

    [Fact]
    public void TryResolveClaimDrops_Level45()
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(45, 0, out var drops);

        Assert.True(resolved);
        TribeGroundItemDrop[] expected = [new(99700, 1), new(539, 1)];
        Assert.Equal(expected, drops.ToArray());
    }

    [Fact]
    public void TryResolveClaimDrops_Level105_GrantsTwoOfItem539()
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(105, 0, out var drops);

        Assert.True(resolved);
        TribeGroundItemDrop[] expected = [new(845, 1), new(539, 2)];
        Assert.Equal(expected, drops.ToArray());
    }

    // --- M-tier levels (114/120/126/132/138/144), recovered by level-milestone-bonus-item-ids ------------------
    // S04_MyWork02.cpp:11265-11291: the lower three (M2/M8/M14) grant exactly two drops (main item + item 539
    // qty 2); the upper three (M20/M26/M32) additionally grant one unit of item 1458 ("EXP Pill(L) cant trade").

    [Fact]
    public void TryResolveClaimDrops_Level114_LvM2_GrantsItem847PlusTwoOfItem539()
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(LevelMilestoneBonus.LvM2, 0, out var drops);

        Assert.True(resolved);
        TribeGroundItemDrop[] expected = [new(847, 1), new(539, 2)];
        Assert.Equal(expected, drops.ToArray());
    }

    [Fact]
    public void TryResolveClaimDrops_Level120_LvM8_GrantsItem846PlusTwoOfItem539()
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(LevelMilestoneBonus.LvM8, 0, out var drops);

        Assert.True(resolved);
        TribeGroundItemDrop[] expected = [new(846, 1), new(539, 2)];
        Assert.Equal(expected, drops.ToArray());
    }

    [Fact]
    public void TryResolveClaimDrops_Level126_LvM14_GrantsItem848PlusTwoOfItem539()
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(LevelMilestoneBonus.LvM14, 0, out var drops);

        Assert.True(resolved);
        TribeGroundItemDrop[] expected = [new(848, 1), new(539, 2)];
        Assert.Equal(expected, drops.ToArray());
    }

    [Fact]
    public void TryResolveClaimDrops_Level132_LvM20_GrantsItem850PlusItem539PlusItem1458()
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(LevelMilestoneBonus.LvM20, 0, out var drops);

        Assert.True(resolved);
        TribeGroundItemDrop[] expected = [new(850, 1), new(539, 2), new(1458, 1)];
        Assert.Equal(expected, drops.ToArray());
    }

    [Fact]
    public void TryResolveClaimDrops_Level138_LvM26_GrantsItem99699PlusItem539PlusItem1458()
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(LevelMilestoneBonus.LvM26, 0, out var drops);

        Assert.True(resolved);
        TribeGroundItemDrop[] expected = [new(99699, 1), new(539, 2), new(1458, 1)];
        Assert.Equal(expected, drops.ToArray());
    }

    [Fact]
    public void TryResolveClaimDrops_Level144_LvM32_GrantsItem99698PlusItem539PlusItem1458()
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(LevelMilestoneBonus.LvM32, 0, out var drops);

        Assert.True(resolved);
        TribeGroundItemDrop[] expected = [new(99698, 1), new(539, 2), new(1458, 1)];
        Assert.Equal(expected, drops.ToArray());
    }

    [Theory]
    [InlineData((byte)0, 83809)]
    [InlineData((byte)1, 83857)]
    [InlineData((byte)2, 83906)]
    public void TryResolveClaimDrops_Level145_AppendsPreviousTribeItemAsSingleUnit(byte previousTribe,
        int expectedTribeItemId)
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(145, previousTribe, out var drops);

        Assert.True(resolved);
        TribeGroundItemDrop[] expected =
        [
            new(851, 1), new(1022, 10), new(1023, 10), new(1019, 10),
            new(expectedTribeItemId, 1) // quantity 1 (enchant-20 stamp not expressible in the drop model)
        ];
        Assert.Equal(expected, drops.ToArray());
    }

    [Fact]
    public void TryResolveClaimDrops_Level145_OutOfRangePreviousTribe_OmitsTribeItem()
    {
        var resolved = LevelMilestoneBonus.TryResolveClaimDrops(145, 3, out var drops);

        Assert.True(resolved);
        TribeGroundItemDrop[] expected = [new(851, 1), new(1022, 10), new(1023, 10), new(1019, 10)];
        Assert.Equal(expected, drops.ToArray());
    }
}
