using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

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

        var result = LevelProgressionCalculator.ResolveLevelUp(49990, 20, levels);

        Assert.True(result.LeveledUp);
        Assert.Equal(51, result.NewLevel);
        Assert.Equal(5, result.StatPointsGranted);
        Assert.Equal(1, result.SkillPointsGranted);
    }

    [Fact]
    public void ResolveLevelUp_BigGainCrossingNinetyNineAndOneTwelveTiers_SumsEachTierSeparately()
    {
        var levels = BuildLevels(145);

        var result = LevelProgressionCalculator.ResolveLevelUp(94500, 19500, levels);

        Assert.True(result.LeveledUp);
        Assert.Equal(115, result.NewLevel);
        Assert.Equal(20 + 195 + 90, result.StatPointsGranted);
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

        var result = LevelProgressionCalculator.ResolveLevelUp(-100, 1100, levels);

        Assert.True(result.LeveledUp);
        Assert.Equal(2, result.NewLevel);
        Assert.Equal(5, result.StatPointsGranted);
        Assert.Equal(1, result.SkillPointsGranted);
    }

    [Fact]
    public void ResolveLevelUp_AlreadyAtMaxLevel_NoLevelUp()
    {
        var levels = BuildLevels(145);

        var result = LevelProgressionCalculator.ResolveLevelUp(144500, 100_000, levels);

        Assert.False(result.LeveledUp);
        Assert.Equal(145, result.NewLevel);
        Assert.Equal(0, result.StatPointsGranted);
        Assert.Equal(0, result.SkillPointsGranted);
    }
}
