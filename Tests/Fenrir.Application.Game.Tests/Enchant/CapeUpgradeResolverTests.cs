using Fenrir.Application.Game.Enchant;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Enchant;

public class CapeUpgradeResolverTests
{
    [Fact]
    public void InvalidTargetItem_Rejected()
    {
        var result = CapeUpgradeResolver.Resolve(9999, 984, 0, 0, new ScriptedRandomSource(0));

        Assert.Equal(CapeUpgradeResolver.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void InvalidMaterial_Rejected()
    {
        var result = CapeUpgradeResolver.Resolve(1401, 12345, 0, 0, new ScriptedRandomSource(0));

        Assert.Equal(CapeUpgradeResolver.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void BaseCloak_LowTierRoll_SuccessGrantsDragonKingCloak()
    {
        // draw1=0 -> low tier, draw2=0 -> 1406; target is the base 1401 so no Emperor override draw;
        // final draw=0 < probability(1) -> success.
        var result = CapeUpgradeResolver.Resolve(1401, 984, 0, 0, new ScriptedRandomSource(0, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Equal(1406, result.NewItemId);
    }

    [Fact]
    public void BaseCloak_FailureRoll_NoMutation()
    {
        // Same rolls as the success case, but the final draw (1) misses probability(1).
        var result = CapeUpgradeResolver.Resolve(1401, 984, 0, 0, new ScriptedRandomSource(0, 0, 1));

        Assert.Equal(CapeUpgradeResolver.Outcome.Failed, result.Outcome);
    }

    [Fact]
    public void NonBaseTarget_LowTierCandidate_EmperorOverrideRoll_ProducesEmperorCape()
    {
        // draw1=0 -> low tier, draw2=1 -> candidate 1403; target != 1401 and candidate in {1403,1404,1406}
        // so an Emperor-override draw happens: draw3=0 -> override to 94100; final draw=0 -> success.
        var result = CapeUpgradeResolver.Resolve(1403, 984, 0, 0, new ScriptedRandomSource(0, 1, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Equal(94100, result.NewItemId);
    }

    [Fact]
    public void HighTierBranch_NoEmperorOverride_ProducesMountFamilyCape()
    {
        // draw1=1 -> high tier (rand()%4 branch), draw2=2 -> candidate 2228 (never Emperor-eligible);
        // final draw=0 -> success.
        var result = CapeUpgradeResolver.Resolve(1401, 2394, 0, 0, new ScriptedRandomSource(1, 2, 0));

        Assert.True(result.Succeeded);
        Assert.Equal(2228, result.NewItemId);
    }

    [Fact]
    public void HighItemValueCharge_AddsFivePercentProbability()
    {
        // draw1=1, draw2=0 -> candidate 2208; final draw=5: probability without the charge is 1 (fails),
        // with the +5 charge bonus probability is 6 (succeeds).
        var result = CapeUpgradeResolver.Resolve(1401, 984, 0, 1, new ScriptedRandomSource(1, 0, 5));

        Assert.True(result.Succeeded);
        Assert.Equal(2208, result.NewItemId);
    }
}
