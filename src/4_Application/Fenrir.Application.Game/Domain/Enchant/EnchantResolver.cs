using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Enchant;

public static class EnchantResolver
{
    public enum EnchantOutcome
    {
        Rejected,

        Unsealed,

        Success,

        Failed,

        Destroyed,

        Protected,

        ResetToForty,

        NoChange
    }

    public const int RegimeBoundary = 40;

    public const int MaxImprove = 50;

    public const int SafeImproveValue = 20;

    private const byte RareItemType = 3;
    private const byte EliteItemType = 4;

    private const byte WingSort = 6;

    /// <summary>
    ///     "Sweet potato" (field <c>aImproveItemValue</c>, consumable item 513) success bonus: whenever the
    ///     player holds at least one charge, exactly +5 percentage points are added to the enchant success
    ///     probability and one charge is consumed -- inseparably, on ALL three resolution paths (standard,
    ///     wing, advanced), never one without the other. On the standard/wing paths the +5 is applied AFTER
    ///     the floor-to-5 (so a floored 5% becomes 10%, not buried by the floor); on the advanced path there
    ///     is no floor and the +5 stacks on top of the tier/guaranteed base. A guaranteed-success material
    ///     overwrites the standard/wing rate to 100 after the +5 (net 100), whereas on the advanced path the
    ///     100 is set before the +5 (net 105) -- both certain successes, so the +5 is marginally inert there,
    ///     but the charge is consumed regardless. The charge consumption itself (mirrored to the client via
    ///     the S146 <c>ConsumesImproveCharge</c> flag on <see cref="EnchantResult" />) is handled by the
    ///     calling service; this resolver owns the +5 and the flag.
    ///     Réf. <c>Server/ts25zone/S04_MyWork02.cpp:3199-3204</c> (standard/wing, after floor),
    ///     <c>:2951-2957</c> (advanced), <c>:2899-2908,3229-3234,3308-3313</c> (guaranteed-material overwrite
    ///     ordering), <c>Server/Header/Protocol/STRUCT.h:1641</c> (<c>S146SWEET_POTATO = 146</c>).
    /// </summary>
    private const int SweetPotatoSuccessBonus = 5;

    public static EnchantResult Resolve(
        ItemDefinition targetItemDefinition,
        ItemStack targetStack,
        ItemDefinition materialItemDefinition,
        int luck,
        int protectForDestroyCharges,
        int improveItemValueCharges,
        IRandomSource random,
        int protectForDestroy2Charges = 0,
        int protectForWingCharges = 0)
    {
        var targetItem = targetItemDefinition.Item;
        var currentImprove = targetStack.Enchant;

        if (targetItem.Sort is < WingSort or > 29 || targetItem.CheckImprove != 2)
            return Rejected();

        if (currentImprove >= MaxImprove)
            return Rejected();

        var isWing = targetItem.Sort == WingSort;

        // Wings cap at +40 and have no advanced (40 -> 50) regime, so they always resolve through the
        // standard path -- even at exactly +40, where a success is a no-op but a failure can still drop or
        // destroy the wing. Only non-wing items enter the advanced regime.
        var result = !isWing && currentImprove >= RegimeBoundary
            ? ResolveAdvanced(targetItem, materialItemDefinition.Item, currentImprove, luck, protectForDestroyCharges,
                improveItemValueCharges, random, protectForDestroy2Charges)
            : ResolveStandard(targetItem, materialItemDefinition.Item, currentImprove, luck,
                protectForDestroyCharges, improveItemValueCharges, random, isWing, protectForWingCharges);

        return result with { IsWing = isWing };
    }

    private static EnchantResult ResolveStandard(ItemRowDto targetItem,
        ItemRowDto materialItem, byte currentImprove, int luck, int protectForDestroyCharges,
        int improveItemValueCharges, IRandomSource random, bool isWing, int protectForWingCharges)
    {
        if (isWing)
            return materialItem.ItemId == WingEnchantMaterialWhitelist.ProtectedMaterialItemId
                ? ResolveWingProtectedMaterial(currentImprove, luck, improveItemValueCharges, random)
                : ResolveWingStandardMaterial(materialItem, currentImprove, luck, protectForWingCharges,
                    improveItemValueCharges, random);

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

        var consumesImproveCharge = improveItemValueCharges > 0;

        // Sweet-potato +5 is applied AFTER the floor-to-5 and BEFORE the guaranteed-success overwrite, so a
        // floored 5% becomes 10% while a guaranteed material still ends at exactly 100 (the +5 is overwritten,
        // matching legacy's standard/wing order). The charge is consumed either way whenever it is held.
        int p1;
        if (material.ForcesGuaranteedSuccess)
        {
            p1 = 100;
        }
        else
        {
            p1 = Math.Max(5, 103 - newImprove * 3 + luck / 100);
            if (consumesImproveCharge)
                p1 += SweetPotatoSuccessBonus;
        }

        if (random.NextInt32(100) < p1)
            return new EnchantResult(EnchantOutcome.Success, newImprove, material.MoneyCost, false,
                ConsumesImproveCharge: consumesImproveCharge);

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

    private static EnchantResult ResolveWingProtectedMaterial(byte currentImprove, int luck,
        int improveItemValueCharges, IRandomSource random)
    {
        var newImprove = currentImprove + WingEnchantMaterialWhitelist.ProtectedMaterialEnchantValue;

        var consumesImproveCharge = improveItemValueCharges > 0;

        // Wing path (iSort == 6): the sweet-potato +5 is material-independent, so the 8106 safe scroll gets it
        // too, applied after the floor-to-5. Its charge was already being consumed here without the +5 -- the
        // exact "decrement without applying the bonus" invariant the contract requires restoring.
        var p1 = Math.Max(5, 103 - newImprove * 3 + luck / 100);
        if (consumesImproveCharge)
            p1 += SweetPotatoSuccessBonus;

        if (random.NextInt32(100) < p1)
            return new EnchantResult(EnchantOutcome.Success, newImprove, WingEnchantMaterialWhitelist.WingEnchantCpCost,
                false, ConsumesImproveCharge: consumesImproveCharge);

        return new EnchantResult(EnchantOutcome.NoChange, currentImprove,
            WingEnchantMaterialWhitelist.WingEnchantCpCost,
            false, ConsumesImproveCharge: consumesImproveCharge);
    }

    /// <summary>
    ///     Standard (non-scroll) wing enchant with a normal wing material (695/696/698/2397) or the
    ///     guaranteed-success scroll (826). A material not in <see cref="WingEnchantMaterialWhitelist.StandardWingMaterials" />
    ///     (this includes 2387/2392, accepted by legacy's <c>CheckWingEnchantMaterial</c> but carrying no
    ///     improve value) is a hard reject -> the handler disconnects the session, matching legacy's switch
    ///     default. The 50-CP cost is applied by the caller for every processed wing outcome; there is no
    ///     rollback -- cost and material are consumed even on failure or destruction.
    ///     Réf. <c>Server/ts25zone/S04_MyWork02.cpp:3035-3079,3178-3219,3235-3306</c>.
    /// </summary>
    private static EnchantResult ResolveWingStandardMaterial(ItemRowDto materialItem, byte currentImprove,
        int luck, int protectForWingCharges, int improveItemValueCharges, IRandomSource random)
    {
        if (!WingEnchantMaterialWhitelist.StandardWingMaterials.TryGetValue(materialItem.ItemId,
                out var nominalValue))
            return Rejected();

        // Cap the resulting level at exactly +40 (applies to every wing material here; 826's nominal +40 is
        // simply the extreme case, filling any current level up to 40 and no further).
        var value = nominalValue;
        if (currentImprove + value >= RegimeBoundary)
            value = RegimeBoundary - currentImprove;

        var newImprove = currentImprove + value;

        // The sweet-potato charge is consumed whenever the player holds one (it feeds the shared probability
        // block, which runs before the scroll's success override), so 826 consumes it too.
        var consumesImproveCharge = improveItemValueCharges > 0;

        int p1;
        if (materialItem.ItemId == WingEnchantMaterialWhitelist.GuaranteedSuccessScrollItemId)
        {
            p1 = 100;
        }
        else
        {
            p1 = Math.Max(5, 103 - newImprove * 3 + luck / 100);
            if (consumesImproveCharge)
                p1 += SweetPotatoSuccessBonus;
        }

        if (random.NextInt32(100) < p1)
            return new EnchantResult(EnchantOutcome.Success, newImprove, WingEnchantMaterialWhitelist.WingEnchantCpCost,
                false, ConsumesImproveCharge: consumesImproveCharge);

        // Destruction is only possible once the resulting level exceeds the safety threshold (20).
        if (newImprove > SafeImproveValue)
        {
            var p2 = -57 + newImprove * 3 - luck / 100;
            if (p2 <= 5)
                p2 -= 5;
            p2 = Math.Max(0, p2);

            if (random.NextInt32(100) < p2)
            {
                // Dedicated wing protection charge (aProtectForWing, notification S099) absorbs the
                // destruction: the wing survives, drops a single level, and the client is told the new charge
                // count. This charge is DISTINCT from the normal-item protection (aProtectForDestroy) --
                // signalled via ConsumesWingProtectCharge so the caller decrements the right counter. The
                // outcome is Failed (wire code 1), not the normal-item Protected (code 4).
                if (protectForWingCharges > 0)
                {
                    var protectedEnchant = currentImprove > 0 ? currentImprove - 1 : 0;
                    return new EnchantResult(EnchantOutcome.Failed, protectedEnchant,
                        WingEnchantMaterialWhitelist.WingEnchantCpCost, false,
                        ConsumesImproveCharge: consumesImproveCharge, ConsumesWingProtectCharge: true);
                }

                return new EnchantResult(EnchantOutcome.Destroyed, 0, WingEnchantMaterialWhitelist.WingEnchantCpCost,
                    false, ConsumesImproveCharge: consumesImproveCharge);
            }
        }

        // Simple failure (or a destruction roll that missed): drop one level, never below 0.
        var failedEnchant = currentImprove > 0 ? currentImprove - 1 : 0;
        return new EnchantResult(EnchantOutcome.Failed, failedEnchant, WingEnchantMaterialWhitelist.WingEnchantCpCost,
            false, ConsumesImproveCharge: consumesImproveCharge);
    }

    private static EnchantResult ResolveAdvanced(ItemRowDto targetItem,
        ItemRowDto materialItem, byte currentImprove, int luck, int protectForDestroyCharges,
        int improveItemValueCharges, IRandomSource random, int protectForDestroy2Charges)
    {
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
            return Rejected();

        var consumesImproveCharge = improveItemValueCharges > 0;

        // Advanced regime (+40 -> +50) has NO floor-to-5: the +5 stacks directly on the fixed tier base
        // (20/15/10/5) or, for a guaranteed material where 100 is already set, on top of that -> 105 (a
        // certain success either way, so the +5 is marginally inert but the charge is still consumed).
        // Applied after the tier-out-of-range reject above, before the draw.
        if (consumesImproveCharge)
            p1 += SweetPotatoSuccessBonus;

        if (random.NextInt32(100) < p1)
            return new EnchantResult(EnchantOutcome.Success, newImprove, material.MoneyCost, false,
                ConsumesImproveCharge: consumesImproveCharge);

        if (currentImprove == RegimeBoundary + 1)
            return new EnchantResult(EnchantOutcome.ResetToForty, RegimeBoundary, material.MoneyCost, false,
                ConsumesImproveCharge: consumesImproveCharge);

        if (protectForDestroy2Charges > 0)
            return new EnchantResult(EnchantOutcome.Protected, currentImprove, material.MoneyCost, false,
                ConsumesImproveCharge: consumesImproveCharge, ConsumesProtectCharge2: true);

        if (protectForDestroyCharges > 0)
            return new EnchantResult(EnchantOutcome.Protected, currentImprove - 1, material.MoneyCost, true,
                ConsumesImproveCharge: consumesImproveCharge);

        return new EnchantResult(EnchantOutcome.ResetToForty, RegimeBoundary, material.MoneyCost, false,
            ConsumesImproveCharge: consumesImproveCharge);
    }

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
        bool ConsumesProtectCharge2 = false,
        bool ConsumesWingProtectCharge = false)
    {
        public bool ConsumesMaterial => Outcome is not EnchantOutcome.Rejected;
    }
}
