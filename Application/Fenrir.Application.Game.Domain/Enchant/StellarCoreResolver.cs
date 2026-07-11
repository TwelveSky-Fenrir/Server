using Fenrir.Application.Game.Domain.Economy;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Enchant;

/// <summary>
///     Pure resolver for the stellar-core-merge target-type path of CZ_IMPROVE_ITEM_SEND
///     (<c>USE_STELLAR_CORE</c>, live under LNW33 -- Server/Header/Protocol/DEFINE.h:125). No I/O, no Zone
///     dependency. Reached by the op24 dispatch ahead of the ordinary <see cref="EnchantResolver" /> band, when
///     the target is a stellar core; unlike every other IMPROVE_ITEM path this one has no random roll -- it
///     always succeeds if the money suffices.
/// </summary>
/// <remarks>
///     Cited to Server/ts25zone/S04_MyWork02.cpp:2721-2750 and Server/Header/function.h:506-510 (the flat
///     50,000,000 stellar override in <c>GetImproveMoney</c>) plus the shared premium block
///     (function.h:451-461, applied here via <see cref="PremiumPricing" />).
///     <para>
///         "Valid stellar core" is modeled exactly as the two cited gates: the target's own item id is below
///         <see cref="MaxStellarCoreItemIdExclusive" /> (93513) AND the material is the identical item id. The
///         contract this resolver was built from cites no finer "is-a-stellar-core" predicate than those two,
///         so none is invented here -- the service that routes an op24 target into this resolver (rather than
///         into <see cref="EnchantResolver" />/<see cref="CostumeImproveResolver" />) is responsible for the
///         coarse "this target is a stellar core at all" classification, exactly as the legacy's own
///         target-type branch does before the goto.
///     </para>
///     <para>
///         "Incremented to the next core tier" is modeled as item id + 1 (the contract's wording is
///         "incremented"); if a future finding shows the tier step is larger than 1, only
///         <see cref="NextTier" /> changes. The result carries the new item id; the material slot is cleared
///         (quantity semantics: this path consumes the WHOLE material stack, not one unit, since the material
///         is itself a stellar core being merged in -- see <see cref="StellarCoreResult.ClearsMaterialSlot" />).
///     </para>
///     The 1% tribe-bank credit on the debited cost is left to the caller (same Zone-free posture as every
///     other resolver in this cluster -- see <c>EnchantItemService.CreditNpcServiceTribeTax</c>).
/// </remarks>
public static class StellarCoreResolver
{
    public enum StellarCoreOutcome
    {
        /// <summary>A real Quit() condition -- the caller must disconnect, never send a clean failure.</summary>
        Rejected,

        /// <summary>Merge succeeded: target item id advances one tier, material slot clears (ZC result 20).</summary>
        Merged
    }

    /// <summary>The target's item id must be strictly below this to be mergeable (Server/ts25zone/S04_MyWork02.cpp:2721-2735).</summary>
    public const int MaxStellarCoreItemIdExclusive = 93513;

    /// <summary>Flat stellar-core merge cost before the premium discount (Server/Header/function.h:506-510).</summary>
    public const int BaseMergeCost = 50_000_000;

    public static StellarCoreResult Resolve(
        ItemDefinition targetDefinition,
        ItemDefinition materialDefinition,
        bool isPremium)
    {
        var targetItemId = targetDefinition.Item.ItemId;

        // Both cited gates: target below the ceiling, and material identical to target.
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

    /// <summary>
    ///     <see cref="NewTargetItemId" /> is the merged core's item id (target id + 1).
    ///     <see cref="ClearsMaterialSlot" /> is always true on a merge -- the material stellar core is entirely
    ///     consumed, its slot emptied, not decremented by one.
    /// </summary>
    public readonly record struct StellarCoreResult(
        StellarCoreOutcome Outcome,
        int Cost,
        int NewTargetItemId)
    {
        public bool ClearsMaterialSlot => Outcome == StellarCoreOutcome.Merged;
    }
}
