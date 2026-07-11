using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Enchant;

public class CostumeImproveResolverTests
{
    [Fact]
    public void Enchant_AtCap_IsRejected()
    {
        var result = CostumeImproveResolver.ResolveEnchant(CostumeImproveResolver.MaxCostumeImprove,
            CostumeImproveResolver.OrdinaryMaterial, new ScriptedRandomSource(0));

        Assert.Equal(CostumeImproveResolver.CostumeEnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Enchant_UnrecognizedMaterial_IsRejected()
    {
        var result = CostumeImproveResolver.ResolveEnchant(0, 999999, new ScriptedRandomSource(0));

        Assert.Equal(CostumeImproveResolver.CostumeEnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Enchant_OrdinaryMaterial_RollBelowRate_Succeeds()
    {
        var result = CostumeImproveResolver.ResolveEnchant(10, CostumeImproveResolver.OrdinaryMaterial,
            new ScriptedRandomSource(14));

        Assert.Equal(CostumeImproveResolver.CostumeEnchantOutcome.Success, result.Outcome);
        Assert.Equal(11, result.NewImprove);
        Assert.Equal(CostumeImproveResolver.EnchantMoneyCost, result.MoneyCost);
        Assert.Equal(CostumeImproveResolver.EnchantContributionPointCost, result.ContributionPointCost);
        Assert.True(result.MaterialConsumed);
    }

    [Fact]
    public void Enchant_OrdinaryMaterial_RollAtRate_NoChange_MaterialStillConsumed()
    {
        var result = CostumeImproveResolver.ResolveEnchant(10, CostumeImproveResolver.OrdinaryMaterial,
            new ScriptedRandomSource(15));

        Assert.Equal(CostumeImproveResolver.CostumeEnchantOutcome.NoChange, result.Outcome);
        Assert.Equal(10, result.NewImprove);
        Assert.True(result.MaterialConsumed);
        Assert.False(result.ReachedCap);
    }

    [Fact]
    public void Enchant_GuaranteedMaterial_SucceedsEvenOnWorstRoll_NoDrawTaken()
    {
        var result = CostumeImproveResolver.ResolveEnchant(10, CostumeImproveResolver.GuaranteedSuccessMaterial,
            new ScriptedRandomSource(99));

        Assert.Equal(CostumeImproveResolver.CostumeEnchantOutcome.Success, result.Outcome);
        Assert.Equal(11, result.NewImprove);
    }

    [Fact]
    public void Enchant_SuccessLandingAtNinetySix_FlagsReachedCap()
    {
        var result = CostumeImproveResolver.ResolveEnchant(95, CostumeImproveResolver.GuaranteedSuccessMaterial,
            new ScriptedRandomSource(99));

        Assert.Equal(CostumeImproveResolver.CostumeEnchantOutcome.Success, result.Outcome);
        Assert.Equal(96, result.NewImprove);
        Assert.True(result.ReachedCap);
    }

    [Fact]
    public void Enchant_SuccessBelowCap_DoesNotFlagReachedCap()
    {
        var result = CostumeImproveResolver.ResolveEnchant(10, CostumeImproveResolver.GuaranteedSuccessMaterial,
            new ScriptedRandomSource(0));

        Assert.False(result.ReachedCap);
    }

    [Fact]
    public void Swap_ExchangesEnchantValues_AtFlatCost()
    {
        var result = CostumeImproveResolver.ResolveSwap(12, 40);

        Assert.Equal(CostumeImproveResolver.CostumeSwapOutcome.Swapped, result.Outcome);
        Assert.Equal(CostumeImproveResolver.SwapMoneyCost, result.MoneyCost);
        Assert.Equal(40, result.NewImproveA);
        Assert.Equal(12, result.NewImproveB);
    }
}
