namespace Fenrir.Application.Game.Domain.Progression;

public static class TitleContributionCost
{
    public const int MaxTitlePortion = 12;

    public const int RefundTypeFull = 0;

    public const int RefundTypeReduced = 1;

    public static readonly int[] CostTable =
        [800, 1700, 2500, 3400, 4200, 5100, 5900, 6800, 7600, 8500, 9300, 10000, 10000];

    public static int PortionOf(int storedTitle)
    {
        return storedTitle % 100;
    }

    public static int PurchaseStepCost(int currentPortion)
    {
        if (currentPortion is < 0 or > 11)
            return 0;

        return CostTable[currentPortion];
    }

    public static bool TryResolveRefundType(int scrollItemId, out int refundType)
    {
        switch (scrollItemId)
        {
            case 1200 or 8419:
                refundType = RefundTypeFull;
                return true;
            case 1494:
                refundType = RefundTypeReduced;
                return true;
            default:
                refundType = -1;
                return false;
        }
    }

    public static int CumulativeRefund(int storedTitle, int refundType)
    {
        if (refundType is < RefundTypeFull or > RefundTypeReduced)
            return 0;

        var portion = PortionOf(storedTitle);
        if (portion is < 1 or > MaxTitlePortion)
            return 0;

        var total = 0;
        for (var rank = 0; rank < portion; rank++)
            total += CostTable[rank];

        return refundType == RefundTypeReduced ? (int)(total * 0.70f) : total;
    }
}
