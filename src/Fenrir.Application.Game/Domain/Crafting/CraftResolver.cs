using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Crafting;

public static class CraftResolver
{
    public enum DustRecycleOutcome
    {
        Rejected,
        Success
    }

    public enum ElixirOutcome
    {
        Success,

        Failed,

        Rejected
    }

    public enum FeatherTierUpOutcome
    {
        Rejected,
        Success
    }

    public enum JadeOutcome
    {
        Success,

        Rejected
    }

    public enum MountFusionOutcome
    {
        Rejected,
        Upgraded,
        DustConsolation
    }

    public enum StoneMatOutcome
    {
        Rejected,
        Success
    }

    public enum WingAssemblyOutcome
    {
        Rejected,
        Assembled,
        Destroyed
    }

    public enum WingFifthTierOutcome
    {
        Rejected,
        Success,
        DustConsolation
    }

    public enum WingTierRerollOutcome
    {
        Rejected,
        Rerolled,
        DustConsolation
    }

    public static JadeResult ResolveJadeUpgrade(ItemStack material1, ItemStack material2)
    {
        if (material1.ItemId != CraftRecipeCatalog.PurpleJadeItemId ||
            material2.ItemId != CraftRecipeCatalog.PurpleJadeItemId)
            return new JadeResult(JadeOutcome.Rejected, null);

        if (material1.Quantity > 1 || material2.Quantity > 1)
            return new JadeResult(JadeOutcome.Rejected, null);

        var result = material1 with
        {
            ItemId = CraftRecipeCatalog.RedJadeItemId, Quantity = 0, Enchant = 0, Combine = 0, Refine = 0,
            Socket = 0
        };
        return new JadeResult(JadeOutcome.Success, result);
    }

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

    public static StoneMatResult ResolveStoneMatCombine(ItemStack material1, ItemStack material2,
        ItemStack material3, ItemStack material4, IRandomSource random)
    {
        if (material1.ItemId != CraftRecipeCatalog.StoneMatMaterialItemId ||
            material2.ItemId != CraftRecipeCatalog.StoneMatMaterialItemId ||
            material3.ItemId != CraftRecipeCatalog.StoneMatMaterialItemId ||
            material4.ItemId != CraftRecipeCatalog.StoneMatMaterialItemId)
            return new StoneMatResult(StoneMatOutcome.Rejected, 0);

        if (material1.Quantity > 1 || material2.Quantity > 1 || material3.Quantity > 1 || material4.Quantity > 1)
            return new StoneMatResult(StoneMatOutcome.Rejected, 0);

        var pool = CraftRecipeCatalog.StoneMatResultPool;
        return new StoneMatResult(StoneMatOutcome.Success, pool[random.NextInt32(pool.Count)]);
    }

    public static MountFusionResult ResolveMountFusion(int sort, int material1ItemId, int material2ItemId,
        int material3ItemId, int catalystItemId, IRandomSource random)
    {
        var isTier2 = sort == CraftRecipeCatalog.MountFusionTier2Sort;
        var materialPool = isTier2
            ? CraftRecipeCatalog.MountFusionTier2MaterialItemIds
            : CraftRecipeCatalog.MountFusionTier1MaterialItemIds;
        var catalystItemIdRequired = isTier2
            ? CraftRecipeCatalog.MountFusionTier2CatalystItemId
            : CraftRecipeCatalog.MountFusionTier1CatalystItemId;

        if (!materialPool.Contains(material1ItemId) || !materialPool.Contains(material2ItemId) ||
            !materialPool.Contains(material3ItemId) || catalystItemId != catalystItemIdRequired)
            return new MountFusionResult(MountFusionOutcome.Rejected, 0, 0);

        var numerator = isTier2
            ? CraftRecipeCatalog.MountFusionTier2SuccessNumerator
            : CraftRecipeCatalog.MountFusionTier1SuccessNumerator;
        var denominator = isTier2
            ? CraftRecipeCatalog.MountFusionTier2SuccessDenominator
            : CraftRecipeCatalog.MountFusionTier1SuccessDenominator;

        if (random.NextInt32(denominator) < numerator)
        {
            var pool = isTier2
                ? CraftRecipeCatalog.MountFusionTier2ResultPool
                : CraftRecipeCatalog.MountFusionTier1ResultPool;
            return new MountFusionResult(MountFusionOutcome.Upgraded, pool[random.NextInt32(pool.Count)], 0);
        }

        var dustQuantity = isTier2
            ? CraftRecipeCatalog.MountFusionTier2FailureDustQuantity
            : CraftRecipeCatalog.MountFusionTier1FailureDustQuantity;
        return new MountFusionResult(MountFusionOutcome.DustConsolation, CraftRecipeCatalog.DustItemId, dustQuantity);
    }

    public static WingAssemblyResult ResolveWingAssembly(bool isTownZone, bool hasSufficientContributionPoints,
        int material1ItemId, int material2ItemId, int material3ItemId, int catalystItemId, byte previousTribe,
        IRandomSource random)
    {
        if (!isTownZone || !hasSufficientContributionPoints)
            return new WingAssemblyResult(WingAssemblyOutcome.Rejected, 0);

        if (material1ItemId != CraftRecipeCatalog.WingFeatherWhiteItemId ||
            material2ItemId != CraftRecipeCatalog.WingFeatherWhiteItemId ||
            material3ItemId != CraftRecipeCatalog.WingFeatherWhiteItemId ||
            !CraftRecipeCatalog.WingAssemblyCatalystItemIds.Contains(catalystItemId))
            return new WingAssemblyResult(WingAssemblyOutcome.Rejected, 0);

        var roll = random.NextInt32(100);
        var tier = roll switch
        {
            1 => 5,
            2 => 2,
            3 => 3,
            4 => 4,
            >= 5 and <= 15 => 1,
            _ => 0
        };

        if (tier == 0)
            return new WingAssemblyResult(WingAssemblyOutcome.Destroyed, 0);

        return new WingAssemblyResult(WingAssemblyOutcome.Assembled,
            CraftRecipeCatalog.WingTierItemId(tier, previousTribe));
    }

    public static FeatherTierUpResult ResolveFeatherTierUp(int sort, int materialItemId, int materialQuantity)
    {
        var (checkItemId, gainItemId) = sort == CraftRecipeCatalog.FeatherTierUpBlackToGoldSort
            ? (CraftRecipeCatalog.WingFeatherBlackItemId, CraftRecipeCatalog.WingFeatherGoldItemId)
            : (CraftRecipeCatalog.WingFeatherWhiteItemId, CraftRecipeCatalog.WingFeatherBlackItemId);

        if (materialItemId != checkItemId || materialQuantity < CraftRecipeCatalog.FeatherTierUpRequiredQuantity)
            return new FeatherTierUpResult(FeatherTierUpOutcome.Rejected, 0);

        return new FeatherTierUpResult(FeatherTierUpOutcome.Success, gainItemId);
    }

    public static WingTierRerollResult ResolveWingTierReroll(int material1ItemId, int material2ItemId,
        int material3ItemId, int catalystItemId, int catalystQuantity, byte previousTribe, IRandomSource random)
    {
        var tier1ItemId = CraftRecipeCatalog.WingTierItemId(1, previousTribe);
        if (material1ItemId != tier1ItemId || material2ItemId != tier1ItemId || material3ItemId != tier1ItemId ||
            catalystItemId != CraftRecipeCatalog.WingTierRerollCatalystItemId || catalystQuantity < 1)
            return new WingTierRerollResult(WingTierRerollOutcome.Rejected, 0);

        var roll = random.NextInt32(100);
        var tier = roll switch
        {
            < 5 => 5,
            < 10 => 2,
            < 15 => 3,
            < 20 => 4,
            _ => 0
        };

        return tier == 0
            ? new WingTierRerollResult(WingTierRerollOutcome.DustConsolation, CraftRecipeCatalog.DustItemId)
            : new WingTierRerollResult(WingTierRerollOutcome.Rerolled,
                CraftRecipeCatalog.WingTierItemId(tier, previousTribe));
    }

    public static WingFifthTierResult ResolveWingFifthTier(int sort, int material1ItemId, int material2ItemId,
        int material3ItemId, int catalystItemId, IRandomSource random)
    {
        if (sort != CraftRecipeCatalog.WingFifthTierSort)
            return new WingFifthTierResult(WingFifthTierOutcome.Rejected, 0);

        if (material1ItemId != CraftRecipeCatalog.WingFifthMaterialItemId ||
            material2ItemId != CraftRecipeCatalog.WingFifthMaterialItemId ||
            material3ItemId != CraftRecipeCatalog.WingFifthMaterialItemId ||
            catalystItemId != CraftRecipeCatalog.WingFifthCatalystItemId)
            return new WingFifthTierResult(WingFifthTierOutcome.Rejected, 0);

        return random.NextInt32(500) < 50
            ? new WingFifthTierResult(WingFifthTierOutcome.Success, CraftRecipeCatalog.WingFifthResultItemId)
            : new WingFifthTierResult(WingFifthTierOutcome.DustConsolation, CraftRecipeCatalog.DustItemId);
    }

    public static DustRecycleResult ResolveDustRecycle(int sort, int materialItemId, int materialQuantity,
        byte previousTribe, IRandomSource random)
    {
        var threshold = DustRecycleThreshold(sort);
        if (threshold == 0 || materialItemId != CraftRecipeCatalog.DustItemId || materialQuantity < threshold)
            return new DustRecycleResult(DustRecycleOutcome.Rejected, 0);

        var resultItemId = sort switch
        {
            CraftRecipeCatalog.DustRecycleWingSort => PickWingDustTier(random, previousTribe),
            CraftRecipeCatalog.DustRecycleCloakSort => CraftRecipeCatalog.DustRecycleCloakResultItemId,
            CraftRecipeCatalog.DustRecycleAnimalSort => PickFromPool(CraftRecipeCatalog.MountFusionTier1ResultPool,
                random),
            CraftRecipeCatalog.DustRecyclePet1Sort => PickDustPet1(random),
            CraftRecipeCatalog.DustRecyclePet2Sort => PickDustPet2(random),
            _ => 0
        };

        return new DustRecycleResult(DustRecycleOutcome.Success, resultItemId);
    }

    public static int DustRecycleThreshold(int sort)
    {
        return sort switch
        {
            CraftRecipeCatalog.DustRecycleWingSort => CraftRecipeCatalog.DustRecycleWingThreshold,
            CraftRecipeCatalog.DustRecycleCloakSort => CraftRecipeCatalog.DustRecycleCloakThreshold,
            CraftRecipeCatalog.DustRecycleAnimalSort => CraftRecipeCatalog.DustRecycleAnimalThreshold,
            CraftRecipeCatalog.DustRecyclePet1Sort => CraftRecipeCatalog.DustRecyclePet1Threshold,
            CraftRecipeCatalog.DustRecyclePet2Sort => CraftRecipeCatalog.DustRecyclePet2Threshold,
            _ => 0
        };
    }

    private static int PickWingDustTier(IRandomSource random, byte previousTribe)
    {
        var roll = random.NextInt32(20);
        var tier = roll switch
        {
            < 5 => 5,
            < 10 => 2,
            < 15 => 3,
            _ => 4
        };
        return CraftRecipeCatalog.WingTierItemId(tier, previousTribe);
    }

    private static int PickFromPool(IReadOnlyList<int> pool, IRandomSource random)
    {
        return pool[random.NextInt32(pool.Count)];
    }

    private static int PickDustPet1(IRandomSource random)
    {
        var roll = random.NextInt32(25);
        return roll switch
        {
            < 4 => 1006,
            < 8 => 1007,
            < 12 => 1008,
            < 16 => 1009,
            < 20 => 1010,
            _ => 1011
        };
    }

    private static int PickDustPet2(IRandomSource random)
    {
        var roll = random.NextInt32(15);
        return roll switch
        {
            < 1 => 1016,
            < 3 => 1012,
            < 6 => 1013,
            < 9 => 1014,
            _ => 1015
        };
    }

    public readonly record struct JadeResult(JadeOutcome Outcome, ItemStack? ResultStack)
    {
        public bool Succeeded => Outcome == JadeOutcome.Success;
    }

    public readonly record struct ElixirResult(ElixirOutcome Outcome, ItemStack? RemainingMaterial, int? ResultItemId)
    {
        public bool Succeeded => Outcome is ElixirOutcome.Success or ElixirOutcome.Failed;
    }

    public readonly record struct StoneMatResult(StoneMatOutcome Outcome, int ResultItemId)
    {
        public bool Succeeded => Outcome == StoneMatOutcome.Success;
    }

    public readonly record struct MountFusionResult(MountFusionOutcome Outcome, int ResultItemId, int ResultQuantity)
    {
        public bool Succeeded => Outcome != MountFusionOutcome.Rejected;
    }

    public readonly record struct WingAssemblyResult(WingAssemblyOutcome Outcome, int ResultItemId)
    {
        public bool Succeeded => Outcome != WingAssemblyOutcome.Rejected;
    }

    public readonly record struct FeatherTierUpResult(FeatherTierUpOutcome Outcome, int ResultItemId)
    {
        public bool Succeeded => Outcome == FeatherTierUpOutcome.Success;
    }

    public readonly record struct WingTierRerollResult(WingTierRerollOutcome Outcome, int ResultItemId)
    {
        public bool Succeeded => Outcome != WingTierRerollOutcome.Rejected;
    }

    public readonly record struct WingFifthTierResult(WingFifthTierOutcome Outcome, int ResultItemId)
    {
        public bool Succeeded => Outcome != WingFifthTierOutcome.Rejected;
    }

    public readonly record struct DustRecycleResult(DustRecycleOutcome Outcome, int ResultItemId)
    {
        public bool Succeeded => Outcome == DustRecycleOutcome.Success;
    }
}
