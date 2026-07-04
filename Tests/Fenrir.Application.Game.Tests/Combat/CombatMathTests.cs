using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>Covers <see cref="CombatMath" />'s primitives against the C++ formulas in <c>Server/ts25zone/S07_MyGame02.cpp</c>, with hand-computed expected values.</summary>
public class CombatMathTests
{
    [Theory]
    [InlineData(100, 100, 70)] // equal stats -> exactly base 70%
    [InlineData(200, 100, 95)] // 70 + (2-1)*25 = 95
    [InlineData(1000, 100, 99)] // clamps at 99
    [InlineData(100, 200, 45)] // 70 - (2-1)*25 = 45
    [InlineData(100, 1000, 1)] // clamps at 1
    public void ComputeHitChancePercent_MatchesTheVerifiedFormula(int attackSuccess, int attackBlock, int expected)
    {
        Assert.Equal(expected, CombatMath.ComputeHitChancePercent(attackSuccess, attackBlock));
    }

    [Fact]
    public void RollHit_RollBelowChance_Hits()
    {
        var rng = new ScriptedRandomSource(50);
        Assert.True(CombatMath.RollHit(70, rng));
    }

    [Fact]
    public void RollHit_RollAtOrAboveChance_Misses()
    {
        var rng = new ScriptedRandomSource(70);
        Assert.False(CombatMath.RollHit(70, rng));
    }

    [Fact]
    public void RollCritical_ZeroOrNegativeChance_NeverRolls()
    {
        // non-positive chance must short-circuit without consuming a random draw
        var rng = new ScriptedRandomSource(0);
        Assert.False(CombatMath.RollCritical(0, rng));
        Assert.False(CombatMath.RollCritical(-5, rng));
    }

    [Fact]
    public void RollCritical_RollBelowChance_Crits()
    {
        var rng = new ScriptedRandomSource(4);
        Assert.True(CombatMath.RollCritical(5, rng));
    }

    [Fact]
    public void ApplyVariance_EvenDraw_Adds()
    {
        // First draw (%2) = 0 -> add branch; second draw (%11) = 10 -> +10%.
        var rng = new ScriptedRandomSource(0, 10);
        var result = CombatMath.ApplyVariance(1000, rng);
        Assert.Equal(1100, result);
    }

    [Fact]
    public void ApplyVariance_OddDraw_Subtracts()
    {
        var rng = new ScriptedRandomSource(1, 10);
        var result = CombatMath.ApplyVariance(1000, rng);
        Assert.Equal(900, result);
    }

    [Fact]
    public void ApplyVariance_ZeroMagnitude_IsANoOp()
    {
        var rng = new ScriptedRandomSource(0, 0);
        Assert.Equal(1000, CombatMath.ApplyVariance(1000, rng));
    }

    [Fact]
    public void ApplySkillPowerRatio_MatchesCalDamage()
    {
        // CalDamage(1000, 50.0f) = (int)(1000 * 150.0f * 0.01f) = 1500
        Assert.Equal(1500, CombatMath.ApplySkillPowerRatio(1000, 50.0f));
        Assert.Equal(1000, CombatMath.ApplySkillPowerRatio(1000, 0f));
    }

    [Fact]
    public void IsInRange_ExactlyAtDistance_IsInRange()
    {
        Assert.True(CombatMath.IsInRange(0, 0, 0, 185, 0, 0, 185f));
    }

    [Fact]
    public void IsInRange_JustBeyondDistance_IsOutOfRange()
    {
        Assert.False(CombatMath.IsInRange(0, 0, 0, 185.1f, 0, 0, 185f));
    }
}
