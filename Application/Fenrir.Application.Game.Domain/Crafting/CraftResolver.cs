using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>
///     Pure resolver for the two CZ_MAKE_ITEM_SEND recipes this pass implements (<see cref="CraftRecipeCatalog" />'s
///     own remarks). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Every other <c>MK_*</c> family (weighted stone tables, mount/wing fusion, dust recycling, skill-book/pet
///     crafting) is out of scope -- needs data Fenrir does not catalog.
/// </remarks>
public static class CraftResolver
{
    public enum ElixirOutcome
    {
        Success,

        /// <summary>The 80% case -- material is still consumed (the legacy runs consumption before the roll's outcome is known).</summary>
        Failed,

        Rejected
    }

    public enum JadeOutcome
    {
        /// <summary>Always succeeds, no roll.</summary>
        Success,

        Rejected
    }

    /// <summary>
    ///     2 separate Purple Jades (not one stack of 2) become one Red Jade in the first slot; the second slot is
    ///     cleared.
    /// </summary>
    /// <remarks>
    ///     Both slots must hold exactly 1 unit -- without this check a stack of N&gt;1 would upgrade the whole stack for
    ///     the cost of one, a duplication vector.
    /// </remarks>
    public static JadeResult ResolveJadeUpgrade(ItemStack material1, ItemStack material2)
    {
        if (material1.ItemId != CraftRecipeCatalog.PurpleJadeItemId ||
            material2.ItemId != CraftRecipeCatalog.PurpleJadeItemId)
            return new JadeResult(JadeOutcome.Rejected, null);

        if (material1.Quantity > 1 || material2.Quantity > 1)
            return new JadeResult(JadeOutcome.Rejected, null);

        // Quantity/Enchant/Combine/Refine/Socket reset to 0 -- a fresh Red Jade, not one inheriting material1's state.
        var result = material1 with
        {
            ItemId = CraftRecipeCatalog.RedJadeItemId, Quantity = 0, Enchant = 0, Combine = 0, Refine = 0,
            Socket = 0
        };
        return new JadeResult(JadeOutcome.Success, result);
    }

    /// <summary>
    ///     10 units of any one base item -&gt; 20% chance of one random item in [801,806]. Free-slot check happens before
    ///     the roll, so it applies regardless of outcome.
    /// </summary>
    public static ElixirResult ResolveAdvancedElixir(ItemStack material, bool hasFreeInventorySlot,
        IRandomSource random)
    {
        if (!CraftRecipeCatalog.AdvancedElixirBaseItemIds.Contains(material.ItemId))
            return new ElixirResult(ElixirOutcome.Rejected, material, null);

        if (!hasFreeInventorySlot)
            return new ElixirResult(ElixirOutcome.Rejected, material, null);

        if (material.Quantity < CraftRecipeCatalog.AdvancedElixirRequiredQuantity)
            return new ElixirResult(ElixirOutcome.Rejected, material, null);

        var remainingQuantity = material.Quantity - CraftRecipeCatalog.AdvancedElixirRequiredQuantity;
        var remaining = remainingQuantity > 0 ? material with { Quantity = remainingQuantity } : (ItemStack?)null;

        if (random.NextInt32(100) < CraftRecipeCatalog.AdvancedElixirSuccessRatePercent)
        {
            var resultItemId = CraftRecipeCatalog.AdvancedElixirResultBaseItemId +
                               random.NextInt32(CraftRecipeCatalog.AdvancedElixirResultRange);
            return new ElixirResult(ElixirOutcome.Success, remaining, resultItemId);
        }

        return new ElixirResult(ElixirOutcome.Failed, remaining, null);
    }

    public readonly record struct JadeResult(JadeOutcome Outcome, ItemStack? ResultStack)
    {
        public bool Succeeded => Outcome == JadeOutcome.Success;
    }

    public readonly record struct ElixirResult(ElixirOutcome Outcome, ItemStack? RemainingMaterial, int? ResultItemId)
    {
        public bool Succeeded => Outcome is ElixirOutcome.Success or ElixirOutcome.Failed;
    }
}
