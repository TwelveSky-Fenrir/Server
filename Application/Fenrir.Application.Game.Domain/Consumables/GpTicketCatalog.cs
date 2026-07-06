namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Pure item-id lookup for the two "GP ticket" cash-shop currency items -- itemId 723 ("500 gp ticket")
///     and 725 ("100 gp ticket"). No I/O, no Zone dependency: the credited amount is a literal constant per
///     item id in the legacy zone source, not read from any item-catalog/config table -- an exhaustive
///     source search for callers of the downstream cash-credit call found exactly these two call sites and
///     no others.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:5369-5384 (the two case branches, the fixed 500/100
///     amounts) ; Server/ts25zone/S04_MyWork03.cpp:2119 (the controlling switch expression -- reaching
///     case 723/725 requires the item's category/dispatch field to equal 723/725 at runtime; Fenrir dispatches
///     on the resolved item id directly instead of replicating that structural switch, the same idiom this
///     handler already uses for the Guild Scroll and Faction Transfer Scroll item-id lookups).
/// </remarks>
public static class GpTicketCatalog
{
    public const int Ticket500ItemId = 723;
    public const int Ticket100ItemId = 725;

    /// <summary>Returns the fixed cash-shop currency amount to credit, or null when itemId is not a GP ticket.</summary>
    public static int? ResolveCreditAmount(int itemId)
    {
        return itemId switch
        {
            Ticket500ItemId => 500,
            Ticket100ItemId => 100,
            _ => null
        };
    }
}
