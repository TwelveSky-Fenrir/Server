using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Enchant;

public class CapeUpgradeResolverTests
{
    [Fact]
    public void InvalidTargetItem_Rejected()
    {
        var result = CapeUpgradeResolver.Resolve(9999, 984, 0, 0, false, new ScriptedRandomSource(0));

        Assert.Equal(CapeUpgradeResolver.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void InvalidMaterial_Rejected()
    {
        var result = CapeUpgradeResolver.Resolve(1401, 12345, 0, 0, false, new ScriptedRandomSource(0));

        Assert.Equal(CapeUpgradeResolver.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void BaseCloak_LowTierRoll_SuccessGrantsDragonKingCloak()
    {
        var result = CapeUpgradeResolver.Resolve(1401, 984, 0, 0, false, new ScriptedRandomSource(0, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Equal(1406, result.NewItemId);
        Assert.Equal(CapeUpgradeResolver.Cost, result.Cost);
    }

    [Fact]
    public void BaseCloak_FailureRoll_NoMutation()
    {
        var result = CapeUpgradeResolver.Resolve(1401, 984, 0, 0, false, new ScriptedRandomSource(0, 0, 1));

        Assert.Equal(CapeUpgradeResolver.Outcome.Failed, result.Outcome);
    }

    [Fact]
    public void NonBaseTarget_LowTierCandidate_EmperorOverrideRoll_ProducesEmperorCape()
    {
        var result = CapeUpgradeResolver.Resolve(1403, 984, 0, 0, false, new ScriptedRandomSource(0, 1, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Equal(94100, result.NewItemId);
    }

    [Fact]
    public void HighTierBranch_NoEmperorOverride_ProducesMountFamilyCape()
    {
        var result = CapeUpgradeResolver.Resolve(1401, 2394, 0, 0, false, new ScriptedRandomSource(1, 2, 0));

        Assert.True(result.Succeeded);
        Assert.Equal(2228, result.NewItemId);
    }

    [Fact]
    public void HighItemValueCharge_AddsFivePercentProbability()
    {
        var result = CapeUpgradeResolver.Resolve(1401, 984, 0, 1, false, new ScriptedRandomSource(1, 0, 5));

        Assert.True(result.Succeeded);
        Assert.Equal(2208, result.NewItemId);
    }

    [Fact]
    public void PremiumInactive_CostIsFullPrice()
    {
        var result = CapeUpgradeResolver.Resolve(1401, 984, 0, 0, false, new ScriptedRandomSource(0, 0, 1));

        Assert.Equal(CapeUpgradeResolver.Cost, result.Cost);
    }

    [Fact]
    public void PremiumActive_CostIsDiscountedTwentyPercent()
    {
        var result = CapeUpgradeResolver.Resolve(1401, 984, 0, 0, true, new ScriptedRandomSource(0, 0, 1));

        Assert.Equal(16_000_000, result.Cost);
        Assert.Equal(CapeUpgradeResolver.GetCost(true), result.Cost);
    }

    [Fact]
    public void GetCost_PremiumActive_IsTwentyPercentOffBasePrice()
    {
        Assert.Equal(16_000_000, CapeUpgradeResolver.GetCost(true));
        Assert.Equal(CapeUpgradeResolver.Cost, CapeUpgradeResolver.GetCost(false));
    }
}
