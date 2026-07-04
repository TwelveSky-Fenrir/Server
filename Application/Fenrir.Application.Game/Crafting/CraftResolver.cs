using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Inventory;

namespace Fenrir.Application.Game.Crafting;

/// <summary>
///     Pure resolver for the two CZ_MAKE_ITEM_SEND recipes this pass implements (<see cref="CraftRecipeCatalog" />'s
///     own remarks) -- verified against <c>Server/ts25zone/S04_MyWork02.cpp:4256-4467</c>. No I/O, no
///     <see cref="World.Zone" /> dependency.
/// </summary>
/// <remarks>
///     OUT OF SCOPE (documented, not guessed): every other <c>MK_*</c> family -- <c>MK_MATS_01019</c> (a
///     hardcoded WEIGHTED random-stone table, <c>randomStoneMat()</c>, whose exact weights were not located
///     in the read source), the LNW33 mount-fusion families (<c>MK_ANIMAL_NUM_1/2</c>, species-id tables
///     Fenrir does not catalog), the wing families (<c>MK_WING_0..6</c>), and the dust-recycling families
///     (<c>MK_DUST_*</c>) -- all require either an unverified weighted table or an item-id catalog Fenrir
///     does not yet have (mounts/wings/pets). <c>W_MAKE_SKILL_SEND</c> (opcode 30, skill-book crafting) and
///     <c>W_MAKE_PET_SEND</c>/<c>W_MAKE_ITEM2_SEND</c> (opcodes 88/131, pet fusion) are ALSO not implemented
///     here for the same reason.
/// </remarks>
public static class CraftResolver
{
    public enum JadeOutcome
    {
        /// <summary>Both material slots hold <see cref="CraftRecipeCatalog.PurpleJadeItemId" /> -- ALWAYS succeeds, no roll (report 04's own "Taux RÉEL" column is empty for this family: verified, not omitted).</summary>
        Success,

        Rejected
    }

    public readonly record struct JadeResult(JadeOutcome Outcome, ItemStack? ResultStack)
    {
        public bool Succeeded => Outcome == JadeOutcome.Success;
    }

    /// <summary>
    ///     MK_MATS_01024: 2 SEPARATE Purple Jade items (1024) -- NOT one stack of 2 -- become ONE Red Jade
    ///     (1025) in the first material's slot; the second slot is cleared entirely. <c>material1</c>/
    ///     <c>material2</c> are (Page1,Index1)/(Page2,Index2)'s current content.
    /// </summary>
    /// <remarks>
    ///     Both slots are REQUIRED to hold exactly 1 unit (<c>InvQuantityGreater(1,1)</c>/<c>InvQuantityGreater(2,1)</c>,
    ///     active under <c>USE_MATS_999</c>, verified <c>S04_MyWork02.cpp:4476-4478</c> -- Quits if either
    ///     slot's quantity exceeds 1) -- a prior pass here didn't enforce this, so a stack of N&gt;1 Purple
    ///     Jades in either slot would have upgraded the WHOLE stack into N Red Jades for the cost of consuming
    ///     only the second slot, a real item-duplication vector. The result's own quantity field is explicitly
    ///     zeroed (<c>tValue[3]=0</c>, <c>S04_MyWork02.cpp:4483</c> -- <c>aInventory[..][3]</c> IS the quantity
    ///     column per <c>InvQuantityGreater</c>'s own macro body), not inherited from material1 -- preserved
    ///     verbatim (D8) even though it looks unusual for a "successful" result.
    /// </remarks>
    public static JadeResult ResolveJadeUpgrade(ItemStack material1, ItemStack material2)
    {
        if (material1.ItemId != CraftRecipeCatalog.PurpleJadeItemId ||
            material2.ItemId != CraftRecipeCatalog.PurpleJadeItemId)
            return new JadeResult(JadeOutcome.Rejected, null);

        if (material1.Quantity > 1 || material2.Quantity > 1)
            return new JadeResult(JadeOutcome.Rejected, null);

        // SetInv's field list (S04_MyWork02.cpp:4481-4486): result carries slot1's X/Y/serial forward, but
        // Quantity and Enchant/Combine/Refine/Socket (tValue[3]/[4], quantity then the packed iValue) reset to
        // 0 -- a fresh Red Jade, not a jade that somehow inherited the Purple Jade's own quantity/upgrade state.
        var result = material1 with
        {
            ItemId = CraftRecipeCatalog.RedJadeItemId, Quantity = 0, Enchant = 0, Combine = 0, Refine = 0,
            Socket = 0
        };
        return new JadeResult(JadeOutcome.Success, result);
    }

    public enum ElixirOutcome
    {
        /// <summary>20% real roll succeeded -- a new item (801-806) was created; <see cref="ElixirResult.ResultItemId" /> names it.</summary>
        Success,

        /// <summary>The 80% case -- material is STILL consumed (verified: <c>wConsumeInv</c> runs unconditionally, before the roll's outcome is even known), nothing is created.</summary>
        Failed,

        Rejected
    }

    public readonly record struct ElixirResult(ElixirOutcome Outcome, ItemStack? RemainingMaterial, int? ResultItemId)
    {
        public bool Succeeded => Outcome is ElixirOutcome.Success or ElixirOutcome.Failed;
    }

    /// <summary>
    ///     MK_ELIXIR_NEW: 10 units of ANY ONE of <see cref="CraftRecipeCatalog.AdvancedElixirBaseItemIds" />
    ///     -&gt; 20% chance of one random item in [801,806]. <paramref name="hasFreeInventorySlot" /> mirrors
    ///     the legacy's own pre-roll <c>FindEmptyInvenForItem</c> space check (S04_MyWork02.cpp:4419-4424,
    ///     checked against item 1301 -- an apparent copy-paste artifact unrelated to the ACTUAL 801-806
    ///     result range; this port instead requires a free slot generically, since the check happens BEFORE
    ///     the roll is even performed, so it cannot depend on the roll's real outcome either way).
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
}
