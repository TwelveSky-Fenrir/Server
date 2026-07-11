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
///     The costume and stellar-core branches (reached via a `goto` ahead of this band's own slot-type check)
///     live in sibling resolvers, <see cref="CostumeImproveResolver" /> and <see cref="StellarCoreResolver" />,
///     not here: a target outside slot-type 6..29 is <see cref="EnchantOutcome.Rejected" /> by THIS resolver,
///     and the routing service is responsible for dispatching a costume/stellar target to the right sibling
///     before falling back to this band (see those two types' own remarks). Wings (slot-type 6) are resolved by
///     the SAME
///     <see cref="ResolveStandard" />/<see cref="ResolveAdvanced" /> machinery as every other equipment slot
///     in the band (materials, probabilities, and the +40/+41-50 regime split are all shared) -- the only
///     wing-specific difference this resolver surfaces is <see cref="EnchantResult.IsWing" />, which the
///     caller (<c>EnchantItemService</c>) uses to route the already-computed <see cref="EnchantResult.Cost" />
///     to the character's CP resource instead of money/tribe-bank credit. Of the two production-build
///     special-material "no-change" short-circuits, the NON-wing one (item 8101, ZC result 8) is now modeled
///     -- 8101 is <see cref="EnchantMaterialCatalog.StandardMaterial.NoChangeOnFailure" />, so a failed roll
///     returns <see cref="EnchantOutcome.NoChange" /> with the enchant untouched
///     (S04_MyWork02.cpp:3315-3318,3370-3378). The WING one (item 8106, ZC result 9) is now ALSO modeled, via
///     the dedicated <see cref="ResolveWingProtectedMaterial" /> path rather than the shared
///     <see cref="EnchantMaterialCatalog.StandardMaterials" /> table (8106 is wing-only, gated by
///     <see cref="WingEnchantMaterialWhitelist" />'s own Gate 1): the 2026-07-11 supplemental finding
///     (enchant-resolver-wing-8106-ticket) recovered its per-attempt enchant value (flat +1, shared with
///     sibling item 695 at the same case label -- S04_MyWork02.cpp:3051-3056) and its cost (a flat 50
///     contribution points, keyed by the EQUIPPED item's Wing category rather than by material, debited
///     unconditionally before the roll -- :3084-3099, :3222-3237). A failed roll never risks a downgrade or
///     destroy -- it short-circuits straight to <see cref="EnchantOutcome.NoChange" /> just like non-wing
///     8101 (:3259-3267). Sibling item 695 shares the enchant-VALUE assignment but its own FAILURE path is a
///     genuinely different code block that was not observed by this finding -- see
///     <see cref="WingEnchantMaterialWhitelist.SiblingWithSharedEnchantValueItemId" />'s own remarks; it
///     stays <see cref="EnchantOutcome.Rejected" /> (unmodeled) pending a follow-up. The wing-specific
///     enchant-cap realm-wide broadcast (a distinct opcode from
///     the non-wing cap broadcast, per the same contract) is a cross-server relay Fenrir has no equivalent
///     for and is not reproduced, matching the precedent already set for <c>CapeUpgradeResolver</c>'s RANKUP
///     notice and <c>CraftPetHandler</c>'s "notable craft" announcement -- neither broadcast (wing or
///     non-wing) is modeled by this resolver or its caller. This resolver's own caller
///     (<c>EnchantItemService</c>) stands the pair in for with a log-only line
///     (<c>CenterRelayNoticeLog.LogEnchantCap</c>, relay sorts 115/2001) rather than a real client-facing
///     broadcast: a 2026-07-11 confirmation pass closed the question of whether real wording could ever be
///     recovered for it -- both sorts are permanently-empty stub cases in every receiving switch in both
///     <c>ts25center</c> and <c>ts25zone</c>, with no <c>default:</c> fallback either, so no notice was ever
///     finished for these relay sorts even in the legacy server itself. See
///     <c>CenterRelayNoticeLog</c>'s own remarks for the full citation trail.
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
///     <para>
///         <c>aProtectForDestroy2</c> (Absolute Craft Ticket, world.Items 828/837 --
///         <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.ProtectForDestroy2" />, already
///         acquired via <c>UseInventoryItemService.ResolveProtectionCharmAsync</c>'s <c>Destroy2</c> kind) is
///         now modeled per the same 2026-07-11 supplemental finding (enchant-resolver-wing-8106-ticket): in
///         <see cref="ResolveAdvanced" />'s +42..+50 sub-tier only (never at the standalone +41 reset, which
///         always fires first regardless of any charge -- S04_MyWork02.cpp:2979-2994), a failed roll checks
///         this SECOND, distinct protect resource BEFORE the ordinary single-tier <c>protectForDestroyCharges</c>
///         charm below it. Unlike that ordinary charm, it leaves the enchant value completely UNTOUCHED (no
///         decrement) even though the wire result code is the identical 4
///         (<see cref="EnchantOutcome.Protected" />) -- see <see cref="EnchantResult.ConsumesProtectCharge2" />.
///         The standard (+0..+40) tier has no equivalent: both legacy sightings of this same check there sit
///         inside a genuinely commented-out block (S04_MyWork02.cpp:3388-3402,3425-3439), confirmed inert, not
///         wired here either. The charge-count status broadcast the legacy fires alongside (STRUCT.h:1615,
///         packet id 104) is a distinct client-facing status update Fenrir has no equivalent packet for and is
///         not reproduced -- the caller only needs to mirror the decremented counter into
///         <c>PlayerRuntimeState.ProtectForDestroy2</c> via the existing write-behind path, same posture as
///         every other charge counter here.
///     </para>
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

        /// <summary>
        ///     A protect charge absorbed what would have been a destroy -- enchant still decreases by 1 (ZC
        ///     result 4). EXCEPTION: when the Absolute Craft Ticket (<c>aProtectForDestroy2</c>) charge fires
        ///     instead of the ordinary Protection Charm (<see cref="EnchantResult.ConsumesProtectCharge2" />
        ///     true), the enchant is left completely UNTOUCHED -- same ZC result 4, different magnitude. Only
        ///     reachable in the advanced (+41..+50) regime.
        /// </summary>
        Protected,

        /// <summary>+41..+49 failure with no protect charge available -- hard reset to exactly +40 (ZC result 3), NEVER a destroy.</summary>
        ResetToForty,

        /// <summary>
        ///     A "no-change" material (item 8101) failed its success roll: enchant is left untouched -- no
        ///     downgrade, no destroy (ZC result 8 for a non-wing target, 9 for a wing). The caller maps the
        ///     result code by <see cref="EnchantResult.IsWing" />.
        /// </summary>
        NoChange
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
        IRandomSource random,
        int protectForDestroy2Charges = 0)
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
                improveItemValueCharges, random, protectForDestroy2Charges)
            : ResolveStandard(targetItem, materialItemDefinition.Item, currentImprove, luck,
                protectForDestroyCharges, improveItemValueCharges, random, isWing);

        return result with { IsWing = isWing };
    }

    private static EnchantResult ResolveStandard(ItemRowDto targetItem,
        ItemRowDto materialItem, byte currentImprove, int luck, int protectForDestroyCharges,
        int improveItemValueCharges, IRandomSource random, bool isWing)
    {
        // Wing-only Protection material (item 8106) -- gated by WingEnchantMaterialWhitelist's own Gate 1,
        // NOT part of the shared StandardMaterials table below (see this type's own remarks and
        // ResolveWingProtectedMaterial's). Non-wing targets never reach this branch.
        if (isWing && materialItem.ItemId == WingEnchantMaterialWhitelist.ProtectedMaterialItemId)
            return ResolveWingProtectedMaterial(currentImprove, luck, improveItemValueCharges, random);

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

        // Special "no-change" material (8101): the failed roll never downgrades or destroys -- the enchant is
        // left exactly where it was (Server/ts25zone/S04_MyWork02.cpp:3315-3318,3370-3378). Checked before the
        // destroy block below, which it fully short-circuits.
        if (material.NoChangeOnFailure)
            return new EnchantResult(EnchantOutcome.NoChange, currentImprove, material.MoneyCost, false,
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

    /// <summary>
    ///     Wing-only Protection material (item 8106, <see cref="WingEnchantMaterialWhitelist.ProtectedMaterialItemId" />)
    ///     -- a dedicated path outside <see cref="EnchantMaterialCatalog.StandardMaterials" /> since Gate 1 makes
    ///     it wing-exclusive. Flat +<see cref="WingEnchantMaterialWhitelist.ProtectedMaterialEnchantValue" />
    ///     per attempt (currentImprove is always &lt; <see cref="RegimeBoundary" /> here, so the usual +40 clamp
    ///     can never actually trigger for a +1 material and is intentionally omitted). Cost is the flat
    ///     Wing-CATEGORY <see cref="WingEnchantMaterialWhitelist.WingEnchantCpCost" /> (contribution points, not
    ///     money -- the caller routes <see cref="EnchantResult.Cost" /> via <see cref="EnchantResult.IsWing" />),
    ///     charged the same whether the roll succeeds or fails (S04_MyWork02.cpp:3084-3099, 3222-3237). The
    ///     success-roll formula reuses the same shared p1 formula as <see cref="ResolveStandard" /> -- the
    ///     destroy-probability formula is independently established as shared between wings and every other
    ///     equipment slot (see <see cref="WingEnchantMaterialWhitelist" />'s own remarks). On failure, the
    ///     material is consumed but the enchant is left completely untouched -- no destroy-risk roll of any
    ///     kind, the wing analogue of non-wing 8101's own <see cref="EnchantOutcome.NoChange" /> short-circuit
    ///     (S04_MyWork02.cpp:3259-3267).
    /// </summary>
    private static EnchantResult ResolveWingProtectedMaterial(byte currentImprove, int luck,
        int improveItemValueCharges, IRandomSource random)
    {
        var newImprove = currentImprove + WingEnchantMaterialWhitelist.ProtectedMaterialEnchantValue;

        // "Sweet potato" (Lucky Enchant Scroll) is consumed on every rolled attempt, win or lose -- same
        // posture as ResolveStandard/ResolveAdvanced.
        var consumesImproveCharge = improveItemValueCharges > 0;

        var p1 = Math.Max(5, 103 - newImprove * 3 + luck / 100);

        if (random.NextInt32(100) < p1)
            return new EnchantResult(EnchantOutcome.Success, newImprove, WingEnchantMaterialWhitelist.WingEnchantCpCost,
                false, ConsumesImproveCharge: consumesImproveCharge);

        return new EnchantResult(EnchantOutcome.NoChange, currentImprove, WingEnchantMaterialWhitelist.WingEnchantCpCost,
            false, ConsumesImproveCharge: consumesImproveCharge);
    }

    private static EnchantResult ResolveAdvanced(ItemRowDto targetItem,
        ItemRowDto materialItem, byte currentImprove, int luck, int protectForDestroyCharges,
        int improveItemValueCharges, IRandomSource random, int protectForDestroy2Charges)
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

        // Absolute Craft Ticket (aProtectForDestroy2, PlayerRuntimeState.ProtectForDestroy2) -- a SECOND,
        // distinct protect resource from the ordinary Protection Charm below, checked first. Unlike that
        // charm it leaves the enchant value completely UNTOUCHED (no decrement) even though the wire result
        // code is the identical 4 -- only reachable in this +42..+50 sub-tier (the +41 case above already
        // returned unconditionally). See this type's own <remarks> for citations.
        if (protectForDestroy2Charges > 0)
            return new EnchantResult(EnchantOutcome.Protected, currentImprove, material.MoneyCost, false,
                ConsumesImproveCharge: consumesImproveCharge, ConsumesProtectCharge2: true);

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
        bool ConsumesImproveCharge = false,
        bool ConsumesProtectCharge2 = false)
    {
        public bool ConsumesMaterial => Outcome is not EnchantOutcome.Rejected;
    }
}
