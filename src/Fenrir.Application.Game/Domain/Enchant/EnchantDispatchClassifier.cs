using Fenrir.Application.Game.Domain.Inventory.UseItems;

namespace Fenrir.Application.Game.Domain.Enchant;

public enum EnchantSubPath
{
    CostumeSwap,

    CostumeEnchant,

    StellarCoreUpgrade,

    Standard
}

public static class EnchantDispatchClassifier
{
    public static EnchantSubPath Classify(int targetItemId, int materialItemId)
    {
        if (CostumeStellarCoreWhitelist.IsValidCostume(targetItemId))
            return CostumeStellarCoreWhitelist.IsValidCostume(materialItemId)
                ? EnchantSubPath.CostumeSwap
                : EnchantSubPath.CostumeEnchant;

        return CostumeStellarCoreWhitelist.IsValidStellarCore(targetItemId)
            ? EnchantSubPath.StellarCoreUpgrade
            : EnchantSubPath.Standard;
    }
}
