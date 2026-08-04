using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Economy;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Forge;

public static class CombineResolver
{
    public const byte RareItemType = 3;
    public const byte EliteItemType = 4;
    public const int MaxCombine = 12;
    private const int RequiredSetItemImprove = 44;

    private static readonly int[] AddMoneySetItemTable =
    [
        100_000_000, 150_000_000, 200_000_000, 250_000_000, 300_000_000, 350_000_000,
        400_000_000, 450_000_000, 500_000_000, 550_000_000, 600_000_000, 650_000_000
    ];

    private static readonly int[] AddMoneyNormalTable =
    [
        1_000_000, 1_500_000, 2_000_000, 2_500_000, 3_000_000, 3_500_000,
        4_000_000, 4_500_000, 5_000_000, 5_500_000, 6_000_000, 6_500_000
    ];

    public static CombineResult Resolve(
        ItemRowDto targetItem, ItemStack targetStack,
        ItemRowDto materialItem, ItemStack materialStack,
        int luck, int luckyComboCharges, bool premiumActive, IRandomSource random)
    {
        if (!MatchesStackDefinition(targetItem, targetStack) ||
            !MatchesStackDefinition(materialItem, materialStack) ||
            targetItem.Type is not (RareItemType or EliteItemType) || targetItem.Sort is < 7 or > 29 ||
            targetItem.CheckHighImprove != 2 || targetStack.Combine >= MaxCombine)
            return CombineResult.Rejected;

        var isScroll = materialItem.ItemId is 2001 or 2002 or 2003;

        if (!isScroll && !IsCompatibleEquipmentMaterial(targetItem, targetStack, materialItem, materialStack))
            return CombineResult.Rejected;

        var cost = GetAddMoney(targetItem, targetStack.Combine, premiumActive);

        int probability;
        var luckyChargeConsumed = false;

        if (isScroll)
        {
            probability = materialItem.ItemId switch { 2001 => 50, 2002 => 80, _ => 100 };
        }
        else
        {
            probability = 65 - targetStack.Combine * 5;
            probability += (int)(luck / 300.0f);

            if (luckyComboCharges > 0)
            {
                probability += 5;
                luckyChargeConsumed = true;
            }
        }

        var success = random.NextInt32(100) < probability;

        if (success)
            return new CombineResult(0, cost, targetStack.Combine + 1, true, luckyChargeConsumed);

        return isScroll
            ? new CombineResult(3, cost, targetStack.Combine, true, luckyChargeConsumed)
            : new CombineResult(1, cost, targetStack.Combine, false, luckyChargeConsumed);
    }

    private static int GetAddMoney(ItemRowDto targetItem, int combine, bool premiumActive)
    {
        if (targetItem.CheckSetItem == 2)
            return AddMoneySetItemTable[combine];

        return PremiumPricing.ApplyPremiumDiscount(AddMoneyNormalTable[combine], premiumActive);
    }

    private static bool MatchesStackDefinition(ItemRowDto item, ItemStack stack)
    {
        return item.ItemId > 0 && stack.ItemId == item.ItemId;
    }

    private static bool IsCompatibleEquipmentMaterial(ItemRowDto targetItem, ItemStack targetStack,
        ItemRowDto materialItem, ItemStack materialStack)
    {
        if (materialItem.Type is not (RareItemType or EliteItemType) || materialItem.Sort is < 7 or > 29 ||
            materialItem.CheckHighImprove != 2 || targetItem.Sort != materialItem.Sort ||
            targetItem.Type != materialItem.Type || targetItem.CheckSetItem != materialItem.CheckSetItem ||
            !IsUnmodified(materialStack))
            return false;

        if (targetItem.CheckSetItem == 2 && targetStack.Enchant < RequiredSetItemImprove)
            return false;

        return targetItem.Type switch
        {
            RareItemType => EffectiveLevel(targetItem) == EffectiveLevel(materialItem),
            EliteItemType => MatchesEliteTier(targetItem, materialItem),
            _ => false
        };
    }

    private static bool IsUnmodified(ItemStack materialStack)
    {
        return materialStack is
        {
            Enchant: 0,
            Combine: 0,
            Refine: 0,
            Socket: 0,
            SocketGem1: 0,
            SocketGem2: 0,
            SocketGem3: 0
        };
    }

    private static int EffectiveLevel(ItemRowDto item)
    {
        return item.Level + item.MartialLevel;
    }

    private static bool MatchesEliteTier(ItemRowDto targetItem, ItemRowDto materialItem)
    {
        return (EffectiveLevel(targetItem), EffectiveLevel(materialItem)) switch
        {
            (100, 95) => true,
            (110, 105) => true,
            (113 or 115, 114) => true,
            (118, 117) => true,
            (121, 120) => true,
            (124, 123) => true,
            (127, 126) => true,
            (130, 129) => true,
            (133, 132) => true,
            (136, 135) => true,
            (139, 138) => true,
            (142, 141) => true,
            (145, 144 or 145) => true,
            _ => false
        };
    }

    public readonly record struct CombineResult(
        int ResultCode,
        int Cost,
        int NewCombine,
        bool MaterialConsumed,
        bool ConsumesLuckyCharge)
    {
        public static readonly CombineResult Rejected = new(-1, 0, 0, false, false);

        public bool IsRejected => ResultCode < 0;
    }
}
