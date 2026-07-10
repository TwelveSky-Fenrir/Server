using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers <see cref="ReflectResolver" /> (destroyer roll + reflect roll, pre-main) --
///     <c>Server/ts25zone/S07_MyGame02.cpp:1267-1334</c>.
/// </summary>
public class ReflectResolverTests
{
    [Fact]
    public void Destroyer_OutOfActiveRange_NeverRolls()
    {
        // Buff 0 is below the [1, 201] active band -- no draw, no success, and no draw consumed for reflect
        // either (reflect buff 0 also out of its [1, 150] band).
        var outcome = ReflectResolver.Resolve(0, 0, 0, 0, 0, true, 100, new ScriptedRandomSource(0));
        Assert.False(outcome.DestroyerSucceeded);
        Assert.False(outcome.ReflectFired);
    }

    [Fact]
    public void Destroyer_RollBelowStrength_Succeeds()
    {
        // strength 201, roll 0 on a 1000 base -> 0 < 201 -> success.
        var outcome = ReflectResolver.Resolve(201, 0, 0, 0, 0, true, 100, new ScriptedRandomSource(0));
        Assert.True(outcome.DestroyerSucceeded);
    }

    [Fact]
    public void Destroyer_RollAtOrAboveStrength_Fails()
    {
        // strength 50, roll 900 on a 1000 base -> 900 < 50 is false.
        var outcome = ReflectResolver.Resolve(50, 0, 0, 0, 0, true, 100, new ScriptedRandomSource(900));
        Assert.False(outcome.DestroyerSucceeded);
    }

    [Fact]
    public void Reflect_GateRollZeroAndProbabilityBelow_Fires_At150PercentOfMainDamage()
    {
        // No destroyer (buff 0). Reflect buff 150, no level gap, no anti-reflect: probability = 150.
        // Draws in order: probability roll (1500 base) = 0 (< 150), gate roll (5 base) = 0 (== 0) -> fires.
        var outcome = ReflectResolver.Resolve(0, 150, 42, 42, 0, true, 160, new ScriptedRandomSource(0, 0));
        Assert.True(outcome.ReflectFired);
        Assert.Equal(240, outcome.ReflectDamage); // 160 * 3 / 2
    }

    [Fact]
    public void Reflect_GateRollNonZero_DoesNotFire()
    {
        // probability roll 0 (< 150) but gate roll 1 (!= 0) -> no fire.
        var outcome = ReflectResolver.Resolve(0, 150, 42, 42, 0, true, 160, new ScriptedRandomSource(0, 1));
        Assert.False(outcome.ReflectFired);
        Assert.Equal(0, outcome.ReflectDamage);
    }

    [Fact]
    public void Reflect_ProbabilityRollAtOrAboveProbability_DoesNotFire()
    {
        // probability roll 200 (>= 150) even though gate roll would be 0 -> no fire.
        var outcome = ReflectResolver.Resolve(0, 150, 42, 42, 0, true, 160, new ScriptedRandomSource(200, 0));
        Assert.False(outcome.ReflectFired);
    }

    [Fact]
    public void Reflect_DisabledForServerType_NeverRolls()
    {
        // reflectEnabled false (zone 124) -> reflect never evaluated regardless of the buff/rolls.
        var outcome = ReflectResolver.Resolve(0, 150, 42, 42, 0, false, 160, new ScriptedRandomSource(0, 0));
        Assert.False(outcome.ReflectFired);
    }

    [Fact]
    public void Reflect_AttackerOutlevelsDefender_ReducesProbabilityByThreePerLevel()
    {
        // Reflect buff 10, attacker 4 levels above defender -> probability = 10 - 3*4 = -2 -> clamped to 0.
        // Even a probability roll of 0 can never be < 0, so reflect can never fire here.
        var outcome = ReflectResolver.Resolve(0, 10, 46, 42, 0, true, 160, new ScriptedRandomSource(0, 0));
        Assert.False(outcome.ReflectFired);
    }
}
