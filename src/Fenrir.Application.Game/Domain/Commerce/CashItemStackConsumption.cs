namespace Fenrir.Application.Game.Domain.Commerce;

public static class CashItemStackConsumption
{
    private const byte StackableSort = 2;
    private const byte StackableSortExtended = 99;

    public static bool IsStackSafe(byte itemSort)
    {
        return itemSort is StackableSort or StackableSortExtended;
    }

    public static int RemainingQuantity(byte itemSort, int currentQuantity)
    {
        return IsStackSafe(itemSort) ? Math.Max(0, currentQuantity - 1) : 0;
    }
}
