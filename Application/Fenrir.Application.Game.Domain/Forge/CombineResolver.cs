using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Forge;

/// <summary>
///     Pure resolver for CZ_ADD_ITEM_SEND (S04_MyWork02.cpp:3453), the "combine" (IUValue) mechanic. No I/O, no
///     Zone dependency.
/// </summary>
/// <remarks>
///     The material-vs-target `iSort != iSort` check (S04_MyWork02.cpp:3512) compares a value to itself and is
///     therefore always false in the legacy -- verified dead code, not reproduced here. <c>aAddItemValue</c>
///     (the "lucky combine" +5% charge) has no acquisition path yet, so <see cref="Resolve" /> is always called
///     with 0 charges, same open issue as <see cref="Enchant.EnchantResolver" />'s <c>protectForDestroyCharges</c>.
///     <c>AddTribeBankInfo2</c>'s 1% cost skim into the tribe bank is not reproduced: it targets an entirely
///     unimplemented "tribe symbol battle" world subsystem (<c>mWorldInfo-&gt;mTribeSymbol</c>), invisible to
///     the paying character either way -- same gap as <c>RerollResolver</c>/<c>RankChangeResolver</c>.
/// </remarks>
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
        int luck, int luckyComboCharges, IRandomSource random)
    {
        if (targetItem.Type is not (RareItemType or EliteItemType) || targetItem.Sort is < 7 or > 29 ||
            targetItem.CheckHighImprove != 2 || targetStack.Combine >= MaxCombine)
            return CombineResult.Rejected;

        var isScroll = materialItem.ItemId is 2001 or 2002 or 2003;

        if (!isScroll)
        {
            if (materialItem.Type is not (RareItemType or EliteItemType) || materialItem.Sort is < 7 or > 29 ||
                materialItem.CheckHighImprove != 2 || (materialItem.CheckSetItem != 2 &&
                                                       (materialStack.Enchant > 0 || materialStack.Combine > 0)))
                return CombineResult.Rejected;

            if (targetItem.CheckSetItem == 2 || materialItem.CheckSetItem == 2)
                if (targetItem.CheckSetItem != materialItem.CheckSetItem || targetItem.Type != materialItem.Type ||
                    targetStack.Enchant < RequiredSetItemImprove || materialStack.Combine > 0)
                    return CombineResult.Rejected;

            if (targetItem.Type == RareItemType)
            {
                if (materialItem.Level + materialItem.MartialLevel != targetItem.Level + targetItem.MartialLevel)
                    return CombineResult.Rejected;
            }
            else if (!MatchesEliteTier(targetItem.Level, materialItem.Level, materialItem.MartialLevel))
            {
                return CombineResult.Rejected;
            }
        }

        var cost = GetAddMoney(targetItem, targetStack.Combine);

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

    private static int GetAddMoney(ItemRowDto targetItem, int combine)
    {
        var table = targetItem.CheckSetItem == 2 ? AddMoneySetItemTable : AddMoneyNormalTable;
        return table[combine];
    }

    /// <summary>
    ///     eq.Level -&gt; required mat.Level for the Elite (IELITE) combine family. Level 145's own
    ///     martial-&gt;martial branch (S04_MyWork02.cpp:3661) compares a value to itself and is always true --
    ///     verified dead code, reproduced here as an unconditional pass. Any eq.Level not in this table has no
    ///     `case` in the legacy switch (no `default:`), so it silently passes too.
    /// </summary>
    private static bool MatchesEliteTier(short eqLevel, short matLevel, byte matMartialLevel)
    {
        return eqLevel switch
        {
            100 => matLevel == 95,
            110 => matLevel == 105,
            113 or 115 => matLevel == 114,
            118 => matLevel == 117,
            121 => matLevel == 120,
            124 => matLevel == 123,
            127 => matLevel == 126,
            130 => matLevel == 129,
            133 => matLevel == 132,
            136 => matLevel == 135,
            139 => matLevel == 138,
            142 => matLevel == 141,
            145 => matMartialLevel >= 1 || matLevel is 144 or 145,
            _ => true
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
