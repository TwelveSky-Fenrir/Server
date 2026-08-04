namespace Fenrir.Application.Game.Domain.Inventory;

public static class ItemQuantityPolicy
{
    public const int MinStackQuantity = 1;

    public const int MaxStackQuantity = 999;

    public const int MaxPetActivity = 100;

    public const byte PetSort = 22;

    public static bool IsStackableSort(byte itemSort)
    {
        return itemSort is 2 or 99;
    }

    public static bool IsPetSort(byte itemSort)
    {
        return itemSort == PetSort;
    }

    public static bool CarriesNoQuantity(byte itemSort)
    {
        return !IsStackableSort(itemSort) && !IsPetSort(itemSort);
    }

    public static int Normalize(byte itemSort, int requestedQuantity)
    {
        if (IsStackableSort(itemSort))
            return requestedQuantity < MinStackQuantity
                ? MinStackQuantity
                : requestedQuantity > MaxStackQuantity
                    ? MaxStackQuantity
                    : requestedQuantity;

        if (IsPetSort(itemSort))
            return requestedQuantity < MinStackQuantity
                ? 0
                : requestedQuantity > MaxPetActivity
                    ? MaxPetActivity
                    : requestedQuantity;

        return 0;
    }

    public static bool IsWithinLegalRange(byte itemSort, int quantity)
    {
        return Normalize(itemSort, quantity) == quantity;
    }
}
