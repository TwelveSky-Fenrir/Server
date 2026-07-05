using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Skills;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.Skills;

/// <summary>
///     Covers <see cref="SkillCatalog.ReturnSkillValue" /> against <c>SKILLSYSTEM::ReturnSkillValue</c> (
///     <c>GameSystem_03_Skill.cpp</c>).
/// </summary>
public class SkillCatalogTests
{
    private static SkillDefinition Skill(byte maxUpgradePoint, short manaUseMin, short manaUseMax,
        short attackInfo1Min = 0, short attackInfo1Max = 0)
    {
        var row = new SkillRowDto(1, "Test", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(1, 0, manaUseMin, 0, 0, 0, 0, 0, attackInfo1Min, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(1, 1, manaUseMax, 0, 0, 0, 0, 0, attackInfo1Max, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty,
            [grade0, grade1]);
    }

    [Fact]
    public void GradePointsBelowOne_ReturnsZero()
    {
        var skill = Skill(10, 10, 100);
        Assert.Equal(0f, SkillCatalog.ReturnSkillValue(skill, 0, SkillValueKind.ManaUse));
        Assert.Equal(0f, SkillCatalog.ReturnSkillValue(skill, -1, SkillValueKind.ManaUse));
    }

    [Fact]
    public void HalfwayGradePoints_InterpolatesLinearly()
    {
        var skill = Skill(10, 10, 100); // min=10, max=100, maxUpgrade=10
        // value = 10 + (100-10)*5/10 = 10 + 45 = 55
        Assert.Equal(55f, SkillCatalog.ReturnSkillValue(skill, 5, SkillValueKind.ManaUse));
    }

    [Fact]
    public void FullGradePoints_ReturnsMaxValue()
    {
        var skill = Skill(10, 10, 100);
        Assert.Equal(100f, SkillCatalog.ReturnSkillValue(skill, 10, SkillValueKind.ManaUse));
    }

    [Fact]
    public void ValueBetweenZeroAndOne_ClampsUpToOne()
    {
        var skill = Skill(10, 0, 1); // at gradePoints=1: 0 + (1-0)*1/10 = 0.1 -> clamped to 1
        Assert.Equal(1f, SkillCatalog.ReturnSkillValue(skill, 1, SkillValueKind.ManaUse));
    }

    [Fact]
    public void ValueAtOrBelowZero_ReturnsZero()
    {
        var skill = Skill(10, 0, 0);
        Assert.Equal(0f, SkillCatalog.ReturnSkillValue(skill, 5, SkillValueKind.ManaUse));
    }

    [Fact]
    public void DifferentKind_ReadsItsOwnColumn()
    {
        var skill = Skill(10, 10, 100, 50, 500);
        // AttackPowerRatio (kind 7 -> AttackInfo1): min=50, max=500, at gradePoints=10 -> 500.
        Assert.Equal(500f, SkillCatalog.ReturnSkillValue(skill, 10, SkillValueKind.AttackPowerRatio));
    }

    [Fact]
    public void MissingGradeRow_ReturnsZero()
    {
        var row = new SkillRowDto(1, "Test", 0, 0, 0, 0, 0, 1, 10, 1, 0);
        var onlyGrade0 = new SkillGradeRowDto(1, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0);
        var skill = new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [onlyGrade0]);

        Assert.Equal(0f, SkillCatalog.ReturnSkillValue(skill, 5, SkillValueKind.ManaUse));
    }

    [Fact]
    public void ZeroMaxUpgradePoint_ReturnsZero_DefensiveGuard()
    {
        var skill = Skill(0, 10, 100);
        Assert.Equal(0f, SkillCatalog.ReturnSkillValue(skill, 1, SkillValueKind.ManaUse));
    }
}
