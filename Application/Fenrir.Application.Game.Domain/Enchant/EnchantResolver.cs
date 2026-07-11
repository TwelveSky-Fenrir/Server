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

        var consumesImproveCharge = improveItemValueCharges > 0;

        var p1 = material.ForcesGuaranteedSuccess
            ? 100
            : Math.Max(5, 103 - newImprove * 3 + luck / 100);

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
        bool ConsumesProtectCharge2 = false)
    {
        public bool ConsumesMaterial => Outcome is not EnchantOutcome.Rejected;
    }
}
