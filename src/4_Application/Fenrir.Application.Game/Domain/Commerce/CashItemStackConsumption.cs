namespace Fenrir.Application.Game.Domain.Commerce;

/// <summary>
///     Cash / stackable-item consumption primitive -- the shared "consume one use of a stack" step reached
///     on the success path of every consumable cash-item case (proxy-shop rental, title/palace upgrade
///     scrolls). Legacy: <c>DecreaseQunatity</c> / <c>IsStackItemSafe</c>.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:593-627.
///     The "stack safe" decision keys on the item CATEGORY (<c>iSort</c>), NOT the item id: an item is
///     stack-safe iff its <c>iSort</c> is 2 or 99 -- the engine's stackable / charged-by-quantity categories
///     (corroborated by the quantity-priced CheckBuyCost*/CheckSellCost paths, Server/Header/function.h:162-212).
///     99 is an additional/extended stackable category alongside 2; no range beyond strict equality to those
///     two is checked. The legacy <c>IsCustomStackItem</c> OR-branch (S04_MyWork03.cpp:588-611) is an inert
///     extension hook returning "not stackable" unconditionally in the shipped build, so it adds nothing to
///     the safe set and is not modeled as an active third condition.
///     <para>
///         <see cref="RemainingQuantity" /> (legacy <c>DecreaseQunatity</c>): a stack-safe item is
///         decremented by exactly 1; every other category (including a null / missing item definition, which
///         is never stack-safe) drops the whole stack to 0 in a single use. The caller clears the slot when
///         the result reaches 0. The distinct unconditional-full-wipe primitive (legacy <c>RemoveItem</c>,
///         S04_MyWork03.cpp:756-761, used by the tribe-reset / faction-transfer scrolls) is NOT this helper --
///         those callers wipe the slot regardless of <c>iSort</c>.
///     </para>
/// </remarks>
public static class CashItemStackConsumption
{
    private const byte StackableSort = 2;
    private const byte StackableSortExtended = 99;

    /// <summary>
    ///     Legacy <c>IsStackItemSafe</c>: true iff the item category (<c>iSort</c>) is a stackable category
    ///     (2 or 99). A null / missing definition is never stack-safe.
    /// </summary>
    public static bool IsStackSafe(byte itemSort)
    {
        return itemSort is StackableSort or StackableSortExtended;
    }

    /// <summary>
    ///     Legacy <c>DecreaseQunatity</c> (unit form): the stack quantity after consuming a single use.
    ///     Stack-safe categories lose exactly 1; every other category loses the whole stack at once. A result
    ///     of 0 means the caller clears the slot.
    /// </summary>
    public static int RemainingQuantity(byte itemSort, int currentQuantity)
    {
        return IsStackSafe(itemSort) ? Math.Max(0, currentQuantity - 1) : 0;
    }
}
