namespace Fenrir.Application.Game.Domain.Consumables;

public static class BulkUseCoercion
{

        public const int MaxStackQuantity = 999;

        public static bool IsBulkRequest(int requestedValue)
    {
        return requestedValue > 1;
    }

        public static int Coerce(int requestedCount, int stackQuantity)
    {
        var count = requestedCount < 1 ? 1 : requestedCount;

        if (count > MaxStackQuantity)
            count = MaxStackQuantity;

        if (count > stackQuantity)
            count = stackQuantity;

        return count;
    }
}
