using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     WORKSTREAM B12-rebirth-sum-recheck (wave14): <c>MyFactor::GetLevel()</c> is <c>aLevel1+aLevel2</c>, not
///     <c>aLevel1</c> alone (Server/Header/Protocol/MyFactor.cpp:508-511) -- every one of the seven
///     level-factor-table lookups <see cref="StatCalculator.ComputeBaseStats" /> resolves through its single
///     shared <c>levelRow</c> (max life/mana/attack power/defense power/attack success/attack block/elemental
///     attack power) must read the combined value, not <see cref="CharacterBaseAttributes.Level" /> alone. These
///     tests exercise <see cref="CharacterBaseAttributes.CombinedLevel" /> through the public
///     <see cref="StatCalculator.ComputeBaseStats" /> entry point, mirroring <see cref="StatCalculatorTests" />'s
///     own high-band-clamp tests (which remain valid as pure <c>Level</c>-only regression tests, since
///     <see cref="CharacterBaseAttributes.Level2" /> defaults to 0 there and so never changes their result).
/// </summary>
public class CombinedLevelStatFeedTests
{
    private static readonly EquippedItemSlot[] NoEquipment = [];

    private static CharacterBaseAttributes Attributes(short level, short level2 = 0)
    {
        return new CharacterBaseAttributes(
            Vitality: 0, Strength: 0, Intelligence: 0, Dexterity: 0,
            Level: level, Tribe: 0, PreviousTribe: 0, Title: 0, Halo: 0, RebirthCount: 0, Level2: level2);
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
        // Level 100 alone would resolve row 100 (life factor 50); the combined level (100+10=110) must resolve
        // row 110 (life factor 999) instead.
        var attributes = Attributes(100, 10);
        var levels = Levels(LevelRow(100, life: 50), LevelRow(110, life: 999));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(999, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_Level2NonZero_FeedsTheSameCombinedRowToAttackPowerToo()
    {
        // The single shared levelRow (StatCalculator.cs's ComputeBaseStats) feeds every one of the seven
        // level-factor formulas, not just MaxLife -- AttackPower is a second, independent witness of the fix.
        var attributes = Attributes(50, 5);
        var levels = Levels(LevelRow(50, attackPower: 11), LevelRow(55, attackPower: 321));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(321, stats.AttackPower);
    }

    [Fact]
    public void ComputeBaseStats_CombinedLevelInHighBand_StillClampsToLevelOneFortyFive()
    {
        // 145 (general cap) + 12 (rebirth-tier cap) = 157, the exact top of the existing high-band clamp range
        // (StatCalculator.GetLevelRow) -- confirms the pre-existing [1,157] bound was already shaped for the
        // combined value, not just coincidentally wide enough for a base level alone.
        var attributes = Attributes(145, 12);
        var levels = Levels(LevelRow(145, life: 777)); // only 145 seeded -- combined 157 must clamp down to it

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(777, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_Level2Zero_ReproducesBaseLevelOnlyLookup()
    {
        // Level2 defaulted/zero (every pre-wave14 caller and test) must reproduce the exact pre-fix result --
        // the non-leakage guarantee that lets every other StatCalculator test stay green through this change.
        var attributes = Attributes(50);
        var levels = Levels(LevelRow(50, life: 4242));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(4242, stats.MaxLife);
    }
}
