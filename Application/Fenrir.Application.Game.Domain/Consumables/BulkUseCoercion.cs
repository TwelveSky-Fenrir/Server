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
        var floored = Math.Max(requestedCount, 1);
        var bounded = Math.Min(Math.Min(floored, stackQuantity), MaxStackQuantity);
        return Math.Max(bounded, 1);
    }
}
