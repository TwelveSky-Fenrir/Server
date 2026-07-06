namespace Fenrir.Application.Game.Domain.Commerce;

/// <summary>
///     Stack-safe-vs-whole-stack consumption rule invoked by the proxy-shop rental-extension item branch of
///     CZ_USE_INVENTORY_ITEM_SEND (op23): a small, fixed set of item categories decrements by exactly one
///     unit per use; every other category loses its entire stacked quantity in a single use.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:613-628 (the quantity-decrement routine) and
///     Server/ts25zone/S04_MyWork03.cpp:593-611 (the stack-safe-category check; the auxiliary "custom stack
///     item" check in that same range unconditionally returns false in the cited code).
///     Whether world.Items 567/592/8422/8423 (the proxy-shop rental-extension items) belong to the
///     stack-safe set is item-data-driven and NOT visible in the cited range -- flagged for
///     cpp-ts25-explorer or an item-data lookup to confirm before relying on either consumption model.
///     <see cref="IsStackSafe" /> defaults to <see langword="false" /> (whole-stack consumption, the
///     non-exceptional majority path per the citation's own framing: "a small fixed set" is stack-safe, the
///     rest is not) until that lookup resolves it either way -- flip it to return <see langword="true" /> for
///     an item id once confirmed stack-safe.
/// </remarks>
public static class CashItemStackConsumption
{
    public static bool IsStackSafe(int itemId)
    {
        return false;
    }

    /// <summary>Quantity remaining in the slot after one use; zero means the whole slot must be cleared.</summary>
    public static int RemainingQuantity(int itemId, int currentQuantity)
    {
        return IsStackSafe(itemId) ? Math.Max(0, currentQuantity - 1) : 0;
    }
}
