using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Enchant;

/// <summary>
///     Pure resolver for CZ_IMPROVE_ITEM_SEND's standard equipment enchant. Covers both +0..+40 and +41..+50 (
///     <c>MAX_IMPROVE_150</c>, verified active for this build). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Wings and the costume/stellar-core branches are out of scope (different item-id ranges Fenrir doesn't catalog) -- a
///     Sort==6 target
///     returns <see cref="EnchantOutcome.NotSupported" />, a clean failure, NOT the legacy's real <c>Quit()</c> that every
///     other
///     <see cref="EnchantOutcome.Rejected" /> reproduces (caller must disconnect on Rejected). The "sweet potato" buff and
///     <c>aProtectForDestroy2</c> are not modeled.
/// </remarks>
public static class EnchantResolver
{
    public enum EnchantOutcome
    {
        /// <summary>A real Quit() condition -- the caller must disconnect, never send a clean failure.</summary>
        Rejected,

        /// <summary>Target is a wing -- Fenrir scope cut, not a legacy Quit(); reply with a clean failure.</summary>
        NotSupported,

        /// <summary>+40 -&gt; +41, no roll (ZC result 0).</summary>
        Unsealed,

        /// <summary>Enchant increased to <see cref="EnchantResult.NewEnchant" /> (ZC result 0).</summary>
        Success,

        /// <summary>Enchant decreased by 1, floored at 0 (ZC result 1).</summary>
        Failed,

        /// <summary>The WHOLE item is destroyed (ZC result 2) -- only reachable in the +0..+40 regime.</summary>
        Destroyed,

        /// <summary>A protect charge absorbed what would have been a destroy -- enchant still decreases by 1 (ZC result 4).</summary>
        Protected,

        /// <summary>+41..+49 failure with no protect charge available -- hard reset to exactly +40 (ZC result 3), NEVER a destroy.</summary>
        ResetToForty
    }

    public const int RegimeBoundary = 40;

    public const int MaxImprove = 50;

    /// <summary>Destroy risk only above this level.</summary>
    public const int SafeImproveValue = 20;

    private const byte RareItemType = 3;
    private const byte EliteItemType = 4;

    public static EnchantResult Resolve(
        ItemDefinition targetItemDefinition,
        ItemStack targetStack,
        ItemDefinition materialItemDefinition,
        int luck,
        int protectForDestroyCharges,
        IRandomSource random)
    {
        var targetItem = targetItemDefinition.Item;
        var currentImprove = targetStack.Enchant;

        if (targetItem.Sort is < 6 or > 29 || targetItem.CheckImprove != 2)
            return Rejected();

        if (currentImprove >= MaxImprove)
            return Rejected();

        if (targetItem.Sort == 6)
            return new EnchantResult(EnchantOutcome.NotSupported, 0, 0, false);

        return currentImprove >= RegimeBoundary
            ? ResolveAdvanced(targetItem, materialItemDefinition.Item, currentImprove, luck, protectForDestroyCharges,
                random)
            : ResolveStandard(targetItem, materialItemDefinition.Item, currentImprove, luck,
                protectForDestroyCharges, random);
    }

    private static EnchantResult ResolveStandard(ItemRowDto targetItem,
        ItemRowDto materialItem, byte currentImprove, int luck, int protectForDestroyCharges,
        IRandomSource random)
    {
        if (!EnchantMaterialCatalog.StandardMaterials.TryGetValue(materialItem.ItemId, out var material))
            return Rejected();

        if (!MatchesTypeRequirement(material.RequiredType, targetItem.Type))
            return Rejected();

        if (material.MaxCurrentImproveExclusive is { } maxCurrent && currentImprove >= maxCurrent)
            return Rejected();

        var value = material.IsFillToValue ? material.Value - currentImprove : material.Value;

        if (!material.IgnoresFortyCap && currentImprove + value >= RegimeBoundary)
            value = RegimeBoundary - currentImprove;

        var newImprove = currentImprove + value;

        var p1 = material.ForcesGuaranteedSuccess
            ? 100
            : Math.Max(5, 103 - newImprove * 3 + luck / 100);

        if (random.NextInt32(100) < p1)
            return new EnchantResult(EnchantOutcome.Success, newImprove, material.MoneyCost, false);

        if (currentImprove + value > SafeImproveValue)
        {
            var p2 = -57 + newImprove * 3 - luck / 100;
            if (p2 <= 5)
                p2 -= 5;
            p2 = Math.Max(0, p2);

            if (random.NextInt32(100) < p2)
            {
                if (protectForDestroyCharges > 0)
                {
                    var protectedEnchant = currentImprove > 0 ? currentImprove - 1 : 0;
                    return new EnchantResult(EnchantOutcome.Protected, protectedEnchant, material.MoneyCost, true);
                }

                return new EnchantResult(EnchantOutcome.Destroyed, 0, material.MoneyCost, false);
            }
        }

        var failedEnchant = currentImprove > 0 ? currentImprove - 1 : 0;
        return new EnchantResult(EnchantOutcome.Failed, failedEnchant, material.MoneyCost, false);
    }

    private static EnchantResult ResolveAdvanced(ItemRowDto targetItem,
        ItemRowDto materialItem, byte currentImprove, int luck, int protectForDestroyCharges,
        IRandomSource random)
    {
        // Item must be Rare/Elite once past +40.
        if (targetItem.Type != RareItemType && targetItem.Type != EliteItemType)
            return Rejected();

        if (currentImprove == RegimeBoundary)
            return materialItem.ItemId == EnchantMaterialCatalog.UnsealItemId
                ? new EnchantResult(EnchantOutcome.Unsealed, RegimeBoundary + 1, 0, false)
                : Rejected();

        if (!EnchantMaterialCatalog.AdvancedMaterials.TryGetValue(materialItem.ItemId, out var material))
            return Rejected();

        var value = material.Value;
        if (currentImprove + value >= MaxImprove)
            value = MaxImprove - currentImprove;

        var newImprove = currentImprove + value;

        var p1 = material.ForcesGuaranteedSuccess ? 100 : TierProbability(newImprove);
        if (p1 < 0)
            return Rejected(); // defensive only -- newImprove outside 41..50 is impossible given the clamp above

        if (random.NextInt32(100) < p1)
            return new EnchantResult(EnchantOutcome.Success, newImprove, material.MoneyCost, false);

        if (currentImprove == RegimeBoundary + 1)
            return new EnchantResult(EnchantOutcome.ResetToForty, RegimeBoundary, material.MoneyCost, false);

        if (protectForDestroyCharges > 0)
            return new EnchantResult(EnchantOutcome.Protected, currentImprove - 1, material.MoneyCost, true);

        return new EnchantResult(EnchantOutcome.ResetToForty, RegimeBoundary, material.MoneyCost, false);
    }

    /// <summary>+41-43: 20% · +44-46: 15% · +47-49: 10% · +50: 5%, keyed by the new (post-attempt) improve value.</summary>
    private static int TierProbability(int newImprove)
    {
        return newImprove switch
        {
            RegimeBoundary + 1 or RegimeBoundary + 2 or RegimeBoundary + 3 => 20,
            RegimeBoundary + 4 or RegimeBoundary + 5 or RegimeBoundary + 6 => 15,
            RegimeBoundary + 7 or RegimeBoundary + 8 or RegimeBoundary + 9 => 10,
            RegimeBoundary + 10 => 5,
            _ => -1
        };
    }

    private static bool MatchesTypeRequirement(EnchantMaterialCatalog.TypeRequirement requirement, byte itemType)
    {
        return requirement switch
        {
            EnchantMaterialCatalog.TypeRequirement.None => true,
            EnchantMaterialCatalog.TypeRequirement.RareOnly => itemType == RareItemType,
            EnchantMaterialCatalog.TypeRequirement.EliteOnly => itemType == EliteItemType,
            EnchantMaterialCatalog.TypeRequirement.RareOrElite => itemType == RareItemType || itemType == EliteItemType,
            _ => false
        };
    }

    private static EnchantResult Rejected()
    {
        return new EnchantResult(EnchantOutcome.Rejected, 0, 0, false);
    }

    public readonly record struct EnchantResult(
        EnchantOutcome Outcome,
        int NewEnchant,
        int Cost,
        bool ConsumesProtectCharge)
    {
        public bool ConsumesMaterial => Outcome is not (EnchantOutcome.Rejected or EnchantOutcome.NotSupported);
    }
}
