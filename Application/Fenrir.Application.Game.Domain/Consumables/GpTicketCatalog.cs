namespace Fenrir.Application.Game.Domain.Consumables;

public static class GpTicketCatalog
{
    public const int Ticket500ItemId = 723;
    public const int Ticket100ItemId = 725;

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
