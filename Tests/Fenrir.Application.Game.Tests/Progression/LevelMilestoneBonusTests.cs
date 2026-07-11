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
    [InlineData(114)]
    [InlineData(120)]
    [InlineData(126)]
    [InlineData(132)]
    [InlineData(138)]
    [InlineData(144)]
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
        Assert.Empty(LevelMilestoneBonus.DeferredMilestoneLevels);
    }

    [Theory]
    [InlineData(44, 45, 45)]
    [InlineData(44, 46, 45)]
    [InlineData(44, 66, 65)]
    [InlineData(0, 145, 145)]
    [InlineData(105, 145, 145)]
    [InlineData(44, 64, 45)]
    [InlineData(105, 114, 114)]
    [InlineData(105, 144, 144)]
    public void ResolveHighestMilestoneCrossed_ReturnsHighestArmableInRange(int previous, int next, int expected)
    {
        Assert.Equal(expected, LevelMilestoneBonus.ResolveHighestMilestoneCrossed(previous, next));
    }

    [Theory]
    [InlineData(45, 45)]
    [InlineData(145, 145)]
    [InlineData(46, 64)]
    [InlineData(106, 113)]
    public void ResolveHighestMilestoneCrossed_ZeroWhenNoneCrossed(int previous, int next)
    {
        Assert.Equal(0, LevelMilestoneBonus.ResolveHighestMilestoneCrossed(previous, next));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(113)]
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
            new(expectedTribeItemId, 1)
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
