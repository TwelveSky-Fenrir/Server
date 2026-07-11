namespace Fenrir.Application.Game.Domain.Commerce;

public static class CashItemStackConsumption
{
    public static bool IsStackSafe(int itemId)
    {
        return false;
    }

        public static int RemainingQuantity(int itemId, int currentQuantity)
    {
        return IsStackSafe(itemId) ? Math.Max(0, currentQuantity - 1) : 0;
    }
}
