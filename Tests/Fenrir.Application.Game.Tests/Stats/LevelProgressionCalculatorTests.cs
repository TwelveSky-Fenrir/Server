using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Hand-computed reference vectors for <see cref="LevelProgressionCalculator" />, matching
///     <c>MyUtil::ProcessForExperience</c>'s level-up loop (<c>S07_MyGame03.cpp:228-296</c>) directly rather than
///     echoing the implementation. Every level in the synthetic table below occupies its own disjoint
///     1000-wide XP band (level N = [(N-1)*1000, N*1000-1]) with a flat 1 skill point (RangeInfo3), so the
///     expected stat/skill totals reduce to simple arithmetic on the number of levels crossed per tier.
/// </summary>
public class LevelProgressionCalculatorTests
{
    private static FrozenDictionary<short, LevelRowDto> BuildLevels(short maxLevel)
    {
        var dict = new Dictionary<short, LevelRowDto>();
        for (short level = 1; level <= maxLevel; level++)
        {
            var min = (level - 1) * 1000;
            var max = level * 1000 - 1;
            dict[level] = new LevelRowDto(level, min, max, 1, 0, 0, 0, 0, 0, 0, 0);
        }

        return dict.ToFrozenDictionary();
    }

    [Fact]
    public void ResolveLevelUp_GainStaysWithinCurrentLevelBand_NoLevelUp()
    {
        var levels = BuildLevels(145);

        // Level 50's band is [49000,49999]; 49500 + 10 = 49510, still inside it.
        var result = LevelProgressionCalculator.ResolveLevelUp(49500, 10, levels);

        Assert.False(result.LeveledUp);
        Assert.Equal(50, result.NewLevel);
        Assert.Equal(0, result.StatPointsGranted);
        Assert.Equal(0, result.SkillPointsGranted);
    }

    [Fact]
    public void ResolveLevelUp_CrossesExactlyOneThreshold_GrantsOneLevelsWorthOfPoints()
    {
        var levels = BuildLevels(145);

        // 49990 (level 50) + 20 = 50010, inside level 51's [50000,50999] band.
        var result = LevelProgressionCalculator.ResolveLevelUp(49990, 20, levels);

        Assert.True(result.LeveledUp);
        Assert.Equal(51, result.NewLevel);
        // Prior level for this single step is 50 (< 99) -> +5.
        Assert.Equal(5, result.StatPointsGranted);
        // RangeInfo3 for level 51 is 1.
        Assert.Equal(1, result.SkillPointsGranted);
    }

    [Fact]
    public void ResolveLevelUp_BigGainCrossingNinetyNineAndOneTwelveTiers_SumsEachTierSeparately()
    {
        var levels = BuildLevels(145);

        // Level 95's band is [94000,94999]; gain of 19500 lands at 114000, level 115's band floor.
        var result = LevelProgressionCalculator.ResolveLevelUp(94500, 19500, levels);

        Assert.True(result.LeveledUp);
        Assert.Equal(115, result.NewLevel);
        // Levels 96-99 (prior 95-98, <99): 4 * 5 = 20.
        // Levels 100-112 (prior 99-111, <112): 13 * 15 = 195.
        // Levels 113-115 (prior 112-114, >=112): 3 * 30 = 90.
        Assert.Equal(20 + 195 + 90, result.StatPointsGranted);
        // 20 levels crossed (96..115), 1 skill point each.
        Assert.Equal(20, result.SkillPointsGranted);
    }

    [Fact]
    public void ResolveLevelUp_ZeroOrNegativeGain_NoLevelUpRegardlessOfTable()
    {
        var levels = BuildLevels(145);

        var result = LevelProgressionCalculator.ResolveLevelUp(94999, 0, levels);

        Assert.False(result.LeveledUp);
        Assert.Equal(95, result.NewLevel);
        Assert.Equal(0, result.StatPointsGranted);
        Assert.Equal(0, result.SkillPointsGranted);
    }

    [Fact]
    public void ResolveLevelUp_ExperienceBelowLevelOneFloor_ResolvesPresentLevelToOne()
    {
        var levels = BuildLevels(145);

        // Never reachable in real data (level 1's own floor is 0), but ReturnLevel's own below-floor guard
        // must still resolve the present level to 1 rather than falling through to MaxLevel.
        var result = LevelProgressionCalculator.ResolveLevelUp(-100, 1100, levels);

        Assert.True(result.LeveledUp);
        Assert.Equal(2, result.NewLevel);
        // Single step, prior level 1 (< 99) -> +5; level 2's RangeInfo3 is 1.
        Assert.Equal(5, result.StatPointsGranted);
        Assert.Equal(1, result.SkillPointsGranted);
    }

    [Fact]
    public void ResolveLevelUp_AlreadyAtMaxLevel_NoLevelUp()
    {
        var levels = BuildLevels(145);

        // Level 145's own band is [144000,144999]; ReturnLevel never resolves past 145 either way.
        var result = LevelProgressionCalculator.ResolveLevelUp(144500, 100_000, levels);

        Assert.False(result.LeveledUp);
        Assert.Equal(145, result.NewLevel);
        Assert.Equal(0, result.StatPointsGranted);
        Assert.Equal(0, result.SkillPointsGranted);
    }
}
