namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Shared "Shift+click bulk use" count-coercion rule reused verbatim by the box-opening, protection-charm,
///     and stat-potion families of <c>CZ_USE_INVENTORY_ITEM_SEND</c> (op 23): the client always sends 0 on a
///     plain click, and a requested count &gt; 1 signals a bulk request. No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:1783-1854 (<c>BulkOpenBoxNoStellar</c> count coercion,
///     the citation this rule was transcribed from) ; Server/Header/Protocol/DEFINE.h:611 (999 stack ceiling,
///     <c>MAX_ITEM_DUPLICATION_NUM</c>).
/// </remarks>
public static class BulkUseCoercion
{
    /// <summary><c>MAX_ITEM_DUPLICATION_NUM</c> (DEFINE.h:611) -- the same ceiling used across every stackable item family.</summary>
    public const int MaxStackQuantity = 999;

    /// <summary>
    ///     A value of zero or one is "not a bulk request" -- the legacy client always sends zero on a plain
    ///     click, and any value &gt; 1 signals a modifier-click bulk-open/bulk-use.
    /// </summary>
    public static bool IsBulkRequest(int requestedValue)
    {
        return requestedValue > 1;
    }

    /// <summary>
    ///     Coerces a client-requested "how many to use at once" count: below one is raised to one, above the
    ///     slot's current stack quantity is lowered to that quantity, above <see cref="MaxStackQuantity" /> is
    ///     lowered to that ceiling. Order matches the source: quantity clamp is applied after the ceiling
    ///     clamp, so the tighter of the two always wins.
    /// </summary>
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
