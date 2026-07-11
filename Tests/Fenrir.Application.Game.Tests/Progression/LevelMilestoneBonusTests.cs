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
    [InlineData(145)]
    public void IsArmableMilestone_TrueForResolvableTiers(int level)
    {
        Assert.True(LevelMilestoneBonus.IsArmableMilestone(level));
    }

    [Theory]
    [InlineData(114)] // LV_M2  -- deferred (unrecoverable claim ids)
    [InlineData(120)] // LV_M8
    [InlineData(126)] // LV_M14
    [InlineData(132)] // LV_M20
    [InlineData(138)] // LV_M26
    [InlineData(144)] // LV_M32
    [InlineData(44)]
    [InlineData(46)]
    [InlineData(0)]
    [InlineData(-1)]
    public void IsArmableMilestone_FalseForDeferredAndNonMilestoneLevels(int level)
    {
        Assert.False(LevelMilestoneBonus.IsArmableMilestone(level));
    }

    [Fact]
    public void DeferredMilestoneLevels_AreNotArmable()
    {
        foreach (var level in LevelMilestoneBonus.DeferredMilestoneLevels)
            Assert.False(LevelMilestoneBonus.IsArmableMilestone(level));
    }

    [Theory]
    [InlineData(44, 45, 45)] // exact single crossing
    [InlineData(44, 46, 45)] // overshoots one milestone
    [InlineData(44, 66, 65)] // crosses 45 AND 65 in one jump -> highest wins
    [InlineData(0, 145, 145)] // crosses every armable milestone -> the top one
    [InlineData(105, 145, 145)]
    [InlineData(44, 64, 45)] // 65 not yet reached
    public void ResolveHighestMilestoneCrossed_ReturnsHighestArmableInRange(int previous, int next, int expected)
    {
        Assert.Equal(expected, LevelMilestoneBonus.ResolveHighestMilestoneCrossed(previous, next));
    }

    [Theory]
    [InlineData(45, 45)] // milestone equals previous level -> not "crossed" (strict lower bound)
    [InlineData(145, 145)]
    [InlineData(46, 64)] // no armable milestone strictly inside the range
    [InlineData(105, 113)] // next armable (145) not yet reached; M-tier tiers are not armable
    public void ResolveHighestMilestoneCrossed_ZeroWhenNoneCrossed(int previous, int next)
    {
        Assert.Equal(0, LevelMilestoneBonus.ResolveHighestMilestoneCrossed(previous, next));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(114)] // an armed-but-deferred level must not resolve a claim
    [InlineData(1)]
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
