using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Combat;

public class DarkAttackPotionResolverTests
{
    [Fact]
    public void AttackerBuffInactive_NeverApplies()
    {
        var result = DarkAttackPotionResolver.ShouldApply(false, 100, false, new ScriptedRandomSource(0));

        Assert.False(result);
    }

    [Fact]
    public void DefenderAlreadyAffected_NeverApplies_EvenWithGuaranteedRoll()
    {
        var result = DarkAttackPotionResolver.ShouldApply(true, 100, true, new ScriptedRandomSource(0));

        Assert.False(result);
    }

    [Fact]
    public void RollBelowProbability_Applies()
    {
        var result = DarkAttackPotionResolver.ShouldApply(true, 50, false, new ScriptedRandomSource(49));

        Assert.True(result);
    }

    [Fact]
    public void RollAtOrAboveProbability_DoesNotApply()
    {
        var result = DarkAttackPotionResolver.ShouldApply(true, 50, false, new ScriptedRandomSource(50));

        Assert.False(result);
    }

    [Fact]
    public void ZeroProbability_NeverApplies()
    {
        var result = DarkAttackPotionResolver.ShouldApply(true, 0, false, new ScriptedRandomSource(0));

        Assert.False(result);
    }
}
