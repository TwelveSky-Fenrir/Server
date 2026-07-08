using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Enchant;

/// <summary>
///     Pure resolver for CZ_IMPROVE_ITEM_SEND's normal-equipment/wings enchant band (target slot-type 6
///     through 29 inclusive -- wings are slot-type 6, everything else in the band is other equipment).
///     Covers both +0..+40 and +41..+50 (<c>MAX_IMPROVE_150</c>, verified active for this build). No I/O, no
///     Zone dependency.
/// </summary>
/// <remarks>
///     The costume/stellar-core branches (reached via a `goto` ahead of this band's own slot-type check, per
///     the contract's cross-cutting gap note) remain out of scope -- a target outside slot-type 6..29 is
///     <see cref="EnchantOutcome.Rejected" /> here, same as before, and the caller must disconnect on
///     <see cref="EnchantOutcome.Rejected" />. Wings (slot-type 6) are now resolved by the SAME
///     <see cref="ResolveStandard" />/<see cref="ResolveAdvanced" /> machinery as every other equipment slot
///     in the band (materials, probabilities, and the +40/+41-50 regime split are all shared) -- the only
///     wing-specific difference this resolver surfaces is <see cref="EnchantResult.IsWing" />, which the
///     caller (<c>EnchantItemService</c>) uses to route the already-computed <see cref="EnchantResult.Cost" />
///     to the character's CP resource instead of money/tribe-bank credit. Two production-build-only
///     special-material short-circuits (a hardcoded material forcing an immediate ZC result 8 on the
///     non-wing path, 9 on the wing path, in place of the destroy roll) are NOT modeled: the contract this
///     resolver was built from cites the switch that contains them (S04_MyWork02.cpp:3222-3450) but does not
///     enumerate either branch's specific hardcoded material item id, so there is nothing to key a
///     short-circuit on without guessing -- flagged for a supplemental legacy-behavior-translator finding
///     rather than invented here. The wing-specific enchant-cap realm-wide broadcast (a distinct opcode from
///     the non-wing cap broadcast, per the same contract) is a cross-server relay Fenrir has no equivalent
///     for and is not reproduced, matching the precedent already set for <c>CapeUpgradeResolver</c>'s RANKUP
///     notice and <c>CraftPetHandler</c>'s "notable craft" announcement -- neither broadcast (wing or
///     non-wing) is modeled by this resolver or its caller.
///     <para>
///         <c>protectForDestroyCharges</c> (Protection Charm, world.Items 1103/1358/1455/8418 --
///         <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.ProtectForDestroy" />) has a real
///         acquisition path via <c>UseInventoryItemService.ResolveProtectionCharmAsync</c> (op23), so
///         <see cref="EnchantOutcome.Protected" /> is reachable in production, not dead code.
///     </para>
///     <para>
///         The "sweet potato" bonus-probability charge (Lucky Enchant Scroll, world.Item 1126 --
///         <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.ImproveItemValue" />, acquired via
///         <c>UseInventoryItemService.ResolveProtectionScrollAsync</c>) is threaded through as
///         <c>improveItemValueCharges</c> and drives <see cref="EnchantResult.ConsumesImproveCharge" /> --
///         consumed on every attempt that actually rolls (win or lose, wing or non-wing), never on a no-roll
///         outcome (<see cref="EnchantOutcome.Unsealed" />) or a precondition rejection. The flat
///         probability-bonus MAGNITUDE the legacy applies for this single attempt is NOT cited anywhere the
///         contract this resolver was built from reached (S04_MyWork02.cpp:3180-3220 is the cited range for
///         the probability formulas themselves, but not the bonus constant), so <see cref="ResolveStandard" />/
///         <see cref="ResolveAdvanced" /> do not add any bonus to <c>p1</c>/<c>p2</c> yet -- flagged for a
///         follow-up legacy-behavior-translator contract citing the exact value. Consuming the charge without
///         yet applying its benefit is the safe interim posture (no dupe/gain path either way), not a guess.
///     </para>
///     <c>aProtectForDestroy2</c> (Absolute Craft Ticket) is not modeled -- the contract's protect-charge side
///     effect names only "equipment protect or wing protect".
/// </remarks>
public static class EnchantResolver
{
    public enum EnchantOutcome
    {
        /// <summary>A real Quit() condition -- the caller must disconnect, never send a clean failure.</summary>
        Rejected,

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

    /// <summary>Wings (Fenrir's only modeled wing-item slot-type).</summary>
    private const byte WingSort = 6;

    public static EnchantResult Resolve(
        ItemDefinition targetItemDefinition,
        ItemStack targetStack,
        ItemDefinition materialItemDefinition,
        int luck,
        int protectForDestroyCharges,
        int improveItemValueCharges,
        IRandomSource random)
    {
        var targetItem = targetItemDefinition.Item;
        var currentImprove = targetStack.Enchant;

        if (targetItem.Sort is < WingSort or > 29 || targetItem.CheckImprove != 2)
            return Rejected();

        if (currentImprove >= MaxImprove)
            return Rejected();

        var isWing = targetItem.Sort == WingSort;

        var result = currentImprove >= RegimeBoundary
            ? ResolveAdvanced(targetItem, materialItemDefinition.Item, currentImprove, luck, protectForDestroyCharges,
                improveItemValueCharges, random)
            : ResolveStandard(targetItem, materialItemDefinition.Item, currentImprove, luck,
                protectForDestroyCharges, improveItemValueCharges, random);

        return result with { IsWing = isWing };
    }

    private static EnchantResult ResolveStandard(ItemRowDto targetItem,
        ItemRowDto materialItem, byte currentImprove, int luck, int protectForDestroyCharges,
        int improveItemValueCharges, IRandomSource random)
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

        // "Sweet potato" (Lucky Enchant Scroll) is consumed on every rolled attempt below, win or lose -- see
        // this type's own <remarks> for why no probability bonus is added yet.
        var consumesImproveCharge = improveItemValueCharges > 0;

        var p1 = material.ForcesGuaranteedSuccess
            ? 100
            : Math.Max(5, 103 - newImprove * 3 + luck / 100);

        if (random.NextInt32(100) < p1)
            return new EnchantResult(EnchantOutcome.Success, newImprove, material.MoneyCost, false,
                ConsumesImproveCharge: consumesImproveCharge);

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
                    return new EnchantResult(EnchantOutcome.Protected, protectedEnchant, material.MoneyCost, true,
                        ConsumesImproveCharge: consumesImproveCharge);
                }

                return new EnchantResult(EnchantOutcome.Destroyed, 0, material.MoneyCost, false,
                    ConsumesImproveCharge: consumesImproveCharge);
            }
        }

        var failedEnchant = currentImprove > 0 ? currentImprove - 1 : 0;
        return new EnchantResult(EnchantOutcome.Failed, failedEnchant, material.MoneyCost, false,
            ConsumesImproveCharge: consumesImproveCharge);
    }

    private static EnchantResult ResolveAdvanced(ItemRowDto targetItem,
        ItemRowDto materialItem, byte currentImprove, int luck, int protectForDestroyCharges,
        int improveItemValueCharges, IRandomSource random)
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

        // Same "consumed on every roll" posture as ResolveStandard above.
        var consumesImproveCharge = improveItemValueCharges > 0;

        if (random.NextInt32(100) < p1)
            return new EnchantResult(EnchantOutcome.Success, newImprove, material.MoneyCost, false,
                ConsumesImproveCharge: consumesImproveCharge);

        if (currentImprove == RegimeBoundary + 1)
            return new EnchantResult(EnchantOutcome.ResetToForty, RegimeBoundary, material.MoneyCost, false,
                ConsumesImproveCharge: consumesImproveCharge);

        if (protectForDestroyCharges > 0)
            return new EnchantResult(EnchantOutcome.Protected, currentImprove - 1, material.MoneyCost, true,
                ConsumesImproveCharge: consumesImproveCharge);

        return new EnchantResult(EnchantOutcome.ResetToForty, RegimeBoundary, material.MoneyCost, false,
            ConsumesImproveCharge: consumesImproveCharge);
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
        bool ConsumesProtectCharge,
        bool IsWing = false,
        bool ConsumesImproveCharge = false)
    {
        public bool ConsumesMaterial => Outcome is not EnchantOutcome.Rejected;
    }
}
