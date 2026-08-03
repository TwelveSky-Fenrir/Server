using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Domain.Enchant;

public static class CostumeImproveResolver
{
    public enum CostumeEnchantOutcome
    {
        Rejected,

        Success,

        NoChange
    }

    public enum CostumeSwapOutcome
    {
        Rejected,

        Swapped
    }

    public const int MaxCostumeImprove = 96;

    public const int OrdinaryMaterial = 8102;

    public const int GuaranteedSuccessMaterial = 724;

    public const int EnchantMoneyCost = 5_000_000;

    public const int EnchantContributionPointCost = 25;

    public const int SwapMoneyCost = 2_000_000_000;

    public static CostumeEnchantResult ResolveEnchant(int currentImprove, int materialItemId, IRandomSource random)
    {
        if (currentImprove >= MaxCostumeImprove)
            return CostumeEnchantResult.Rejected;

        if (materialItemId is not (OrdinaryMaterial or GuaranteedSuccessMaterial))
            return CostumeEnchantResult.Rejected;

        var newImprove = currentImprove + 1;

        bool success;
        if (materialItemId == GuaranteedSuccessMaterial)
        {
            success = true;
        }
        else
        {
            var (successRate, _) = TribeHaloEnchantResolver.GetRates(currentImprove);
            success = random.NextInt32(100) < successRate;
        }

        if (!success)
            return new CostumeEnchantResult(CostumeEnchantOutcome.NoChange, currentImprove,
                EnchantMoneyCost, EnchantContributionPointCost, false);

        return new CostumeEnchantResult(CostumeEnchantOutcome.Success, newImprove,
            EnchantMoneyCost, EnchantContributionPointCost, newImprove == MaxCostumeImprove);
    }

    public static CostumeSwapResult ResolveSwap(int improveA, int improveB)
    {
        if (improveA >= improveB || improveB < 1)
            return CostumeSwapResult.Rejected;

        return new CostumeSwapResult(CostumeSwapOutcome.Swapped, SwapMoneyCost, improveB, improveA);
    }

    public readonly record struct CostumeEnchantResult(
        CostumeEnchantOutcome Outcome,
        int NewImprove,
        int MoneyCost,
        int ContributionPointCost,
        bool ReachedCap)
    {
        public static readonly CostumeEnchantResult Rejected =
            new(CostumeEnchantOutcome.Rejected, 0, 0, 0, false);

        public bool MaterialConsumed => Outcome is not CostumeEnchantOutcome.Rejected;
    }

    public readonly record struct CostumeSwapResult(
        CostumeSwapOutcome Outcome,
        int MoneyCost,
        int NewImproveA,
        int NewImproveB)
    {
        public static readonly CostumeSwapResult Rejected = new(CostumeSwapOutcome.Rejected, 0, 0, 0);
    }
}
