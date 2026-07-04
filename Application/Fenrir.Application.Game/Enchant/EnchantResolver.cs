using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Enchant;

/// <summary>
///     Pure resolver for CZ_IMPROVE_ITEM_SEND's STANDARD equipment enchant (report 04_mega_switches.md §4,
///     verified against <c>Server/ts25zone/S04_MyWork02.cpp:2500-3450</c>). Covers both the +0..+40 regime
///     and the +41..+50 regime (<c>MAX_IMPROVE_150</c> -- verified ACTIVE for this build, DEFINE.h:56/110-114:
///     the <c>#undef</c> is guarded by <c>#ifndef LNW33EU</c>, and <c>LNW33EU</c> IS defined for the EU33
///     reference build). No I/O, no <see cref="World.Zone" /> dependency.
/// </summary>
/// <remarks>
///     OUT OF SCOPE (documented, not guessed): wings (<c>eq.iSort == 6</c>, CP-costed via
///     <c>ContributionPoints</c> instead of Money, different material table) and the costume/stellar-core
///     branches (<c>USE_ENCHANT_COSTUME(_V2)</c>/<c>USE_STELLAR_CORE</c>, both verified active but a
///     COMPLETELY separate mechanic keyed off item-id ranges Fenrir does not catalog) -- a target item whose
///     <c>Sort</c> is 6 returns the DEDICATED <see cref="EnchantOutcome.NotSupported" /> (a Fenrir-side scope
///     cut, NOT a legacy Quit(): the caller should reply with a clean failure, not disconnect a player doing
///     nothing wrong). EVERY OTHER <see cref="EnchantOutcome.Rejected" /> reproduces a REAL <c>Quit()</c> in
///     the verified source (including an unrecognized material -- the source's own <c>default: Quit()</c> in
///     both material-lookup switches) and the caller must disconnect on it. The "sweet potato" buff (<c>aImproveItemValue</c>, +5%
///     success, one-shot consumed) and <c>aProtectForDestroy2</c> (a SEPARATE, more generous protect charge
///     only relevant in the +41..+50 regime) are not modeled -- Fenrir tracks neither counter yet.
/// </remarks>
public static class EnchantResolver
{
    /// <summary><c>MAX_IMPROVE_ITEM_NUM</c> (DEFINE.h:613).</summary>
    public const int RegimeBoundary = 40;

    /// <summary><c>MAX_IMPROVE_ITEM_NUM2</c> (DEFINE.h:614).</summary>
    public const int MaxImprove = 50;

    /// <summary><c>SAFE_IMPROVE_VALUE</c> for LNW33 (S04_MyWork02.cpp:2497) -- destroy risk only above this level.</summary>
    public const int SafeImproveValue = 20;

    private const byte RareItemType = 3;
    private const byte EliteItemType = 4;

    public enum EnchantOutcome
    {
        /// <summary>A REAL verified <c>Quit()</c> condition -- the caller must disconnect, never send a clean failure.</summary>
        Rejected,

        /// <summary>Target is a wing (<c>Sort == 6</c>) -- Fenrir scope cut, NOT a legacy Quit(); reply with a clean failure (see class remarks).</summary>
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

    public readonly record struct EnchantResult(EnchantOutcome Outcome, int NewEnchant, int Cost, bool ConsumesProtectCharge)
    {
        /// <summary>Every terminal outcome consumes the material exactly once (<c>DecreaseMaterial</c>) -- neither <see cref="EnchantOutcome.Rejected" /> nor <see cref="EnchantOutcome.NotSupported" /> does.</summary>
        public bool ConsumesMaterial => Outcome is not (EnchantOutcome.Rejected or EnchantOutcome.NotSupported);
    }

    /// <summary>
    ///     <paramref name="luck" /> is the caster's <c>MyFactor::GetLuck()</c> value (already resolved by the
    ///     caller from <see cref="Stats.EffectiveStats" />); <paramref name="protectForDestroyCharges" /> is
    ///     the character's current <c>ProtectForDestroy</c> count. <paramref name="random" /> supplies ONE
    ///     <c>rand_mir() % 100</c> draw per legacy call site, in the SAME order, matching every other resolver
    ///     in this codebase (<see cref="IRandomSource" />'s own remarks).
    /// </summary>
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
            // Wings -- CP-costed, separate material table, NOT modeled (class remarks). Not the player's
            // fault, so a clean failure rather than a disconnect.
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
                p2 -= 5; // LNW33 (S04_MyWork02.cpp:3210-3213).
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
        // eq.iType must be Rare/Elite once past +40 (CheckEquipType1(eq.iInfo) is the base-equip-sort guard
        // already applied above; the RARE/ELITE requirement only kicks in when USE_ENCHANT_COSTUME is active,
        // which it is -- S04_MyWork02.cpp:2846-2859).
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
            return Rejected(); // newImprove landed outside 41..50 -- structurally impossible given the clamp above, defensive only.

        if (random.NextInt32(100) < p1)
            return new EnchantResult(EnchantOutcome.Success, newImprove, material.MoneyCost, false);

        if (currentImprove == RegimeBoundary + 1)
            return new EnchantResult(EnchantOutcome.ResetToForty, RegimeBoundary, material.MoneyCost, false);

        // aProtectForDestroy2 (a separate, more generous charge only relevant here) is NOT modeled -- see
        // class remarks; only the ORDINARY ProtectForDestroy charge is checked, exactly like the +0..+40 regime.
        if (protectForDestroyCharges > 0)
            return new EnchantResult(EnchantOutcome.Protected, currentImprove - 1, material.MoneyCost, true);

        return new EnchantResult(EnchantOutcome.ResetToForty, RegimeBoundary, material.MoneyCost, false);
    }

    /// <summary>+41-43: 20% · +44-46: 15% · +47-49: 10% · +50: 5% (S04_MyWork02.cpp:2926-2949), keyed by the NEW (post-attempt) improve value.</summary>
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
}
