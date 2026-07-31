using Fenrir.Application.Game.Domain.Economy;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Enchant;

public static class StellarCoreResolver
{
    public enum StellarCoreOutcome
    {
        Rejected,

        Merged
    }

    public const int MaxStellarCoreItemIdExclusive = 93513;

    public const int BaseMergeCost = 50_000_000;

    public static StellarCoreResult Resolve(
        ItemDefinition targetDefinition,
        ItemDefinition materialDefinition,
        bool isPremium)
    {
        var targetItemId = targetDefinition.Item.ItemId;

        if (targetItemId <= 0 || targetItemId >= MaxStellarCoreItemIdExclusive ||
            materialDefinition.Item.ItemId != targetItemId)
            return new StellarCoreResult(StellarCoreOutcome.Rejected, 0, 0);

        var cost = PremiumPricing.ApplyPremiumDiscount(BaseMergeCost, isPremium);

        return new StellarCoreResult(StellarCoreOutcome.Merged, cost, NextTier(targetItemId));
    }

    private static int NextTier(int currentItemId)
    {
        return currentItemId + 1;
    }

    public readonly record struct StellarCoreResult(
        StellarCoreOutcome Outcome,
        int Cost,
        int NewTargetItemId)
    {
        public bool ClearsMaterialSlot => Outcome == StellarCoreOutcome.Merged;
    }
}
