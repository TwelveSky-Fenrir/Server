using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Combat;

public class ReflectResolverTests
{
    [Fact]
    public void Destroyer_OutOfActiveRange_NeverRolls()
    {
        var outcome = ReflectResolver.Resolve(0, 0, 0, 0, 0, true, 100, new ScriptedRandomSource(0));
        Assert.False(outcome.DestroyerSucceeded);
        Assert.False(outcome.ReflectFired);
    }

    [Fact]
    public void Destroyer_RollBelowStrength_Succeeds()
    {
        var outcome = ReflectResolver.Resolve(201, 0, 0, 0, 0, true, 100, new ScriptedRandomSource(0));
        Assert.True(outcome.DestroyerSucceeded);
    }

    [Fact]
    public void Destroyer_RollAtOrAboveStrength_Fails()
    {
        var outcome = ReflectResolver.Resolve(50, 0, 0, 0, 0, true, 100, new ScriptedRandomSource(900));
        Assert.False(outcome.DestroyerSucceeded);
    }

    [Fact]
    public void Reflect_GateRollZeroAndProbabilityBelow_Fires_At150PercentOfMainDamage()
    {
        var outcome = ReflectResolver.Resolve(0, 150, 42, 42, 0, true, 160, new ScriptedRandomSource(0, 0));
        Assert.True(outcome.ReflectFired);
        Assert.Equal(240, outcome.ReflectDamage);
    }

    [Fact]
    public void Reflect_GateRollNonZero_DoesNotFire()
    {
        var outcome = ReflectResolver.Resolve(0, 150, 42, 42, 0, true, 160, new ScriptedRandomSource(0, 1));
        Assert.False(outcome.ReflectFired);
        Assert.Equal(0, outcome.ReflectDamage);
    }

    [Fact]
    public void Reflect_ProbabilityRollAtOrAboveProbability_DoesNotFire()
    {
        var outcome = ReflectResolver.Resolve(0, 150, 42, 42, 0, true, 160, new ScriptedRandomSource(200, 0));
        Assert.False(outcome.ReflectFired);
    }

    [Fact]
    public void Reflect_DisabledForServerType_NeverRolls()
    {
        var outcome = ReflectResolver.Resolve(0, 150, 42, 42, 0, false, 160, new ScriptedRandomSource(0, 0));
        Assert.False(outcome.ReflectFired);
    }

    [Fact]
    public void Reflect_AttackerOutlevelsDefender_ReducesProbabilityByThreePerLevel()
    {
        var outcome = ReflectResolver.Resolve(0, 10, 46, 42, 0, true, 160, new ScriptedRandomSource(0, 0));
        Assert.False(outcome.ReflectFired);
    }
}
