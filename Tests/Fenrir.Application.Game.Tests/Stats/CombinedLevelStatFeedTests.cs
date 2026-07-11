using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

public class CombinedLevelStatFeedTests
{
    private static readonly EquippedItemSlot[] NoEquipment = [];

    private static CharacterBaseAttributes Attributes(short level, short level2 = 0)
    {
        return new CharacterBaseAttributes(
            0, 0, 0, 0,
            level, 0, 0, 0, 0, 0, level2);
    }

    private static FrozenDictionary<short, LevelRowDto> Levels(params LevelRowDto[] rows)
    {
        var dict = new Dictionary<short, LevelRowDto>();
        foreach (var row in rows) dict[row.Level] = row;
        return dict.ToFrozenDictionary();
    }

    private static LevelRowDto LevelRow(short level, int life = 0, short attackPower = 0)
    {
        return new LevelRowDto(level, 0, 100, 0, attackPower, 0, 0, 0, 0, life, 0);
    }

    [Fact]
    public void CombinedLevel_IsBaseLevelPlusLevel2()
    {
        var attributes = Attributes(100, 10);

        Assert.Equal(110, attributes.CombinedLevel);
    }

    [Fact]
    public void ComputeBaseStats_Level2NonZero_UsesCombinedLevelForLevelFactorLookup_NotBaseLevelAlone()
    {
        var attributes = Attributes(100, 10);
        var levels = Levels(LevelRow(100, 50), LevelRow(110, 999));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(999, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_Level2NonZero_FeedsTheSameCombinedRowToAttackPowerToo()
    {
        var attributes = Attributes(50, 5);
        var levels = Levels(LevelRow(50, attackPower: 11), LevelRow(55, attackPower: 321));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(321, stats.AttackPower);
    }

    [Fact]
    public void ComputeBaseStats_CombinedLevelInHighBand_StillClampsToLevelOneFortyFive()
    {
        var attributes = Attributes(145, 12);
        var levels = Levels(LevelRow(145, 777));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(777, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_Level2Zero_ReproducesBaseLevelOnlyLookup()
    {
        var attributes = Attributes(50);
        var levels = Levels(LevelRow(50, 4242));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(4242, stats.MaxLife);
    }
}
