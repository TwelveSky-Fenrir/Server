using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class GpTicketCatalogTests
{
    [Theory]
    [InlineData(GpTicketCatalog.Ticket500ItemId, 500)]
    [InlineData(GpTicketCatalog.Ticket100ItemId, 100)]
    public void ResolveCreditAmount_KnownTicketItemId_ReturnsItsFixedAmount(int itemId, int expectedAmount)
    {
        Assert.Equal(expectedAmount, GpTicketCatalog.ResolveCreditAmount(itemId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(722)]
    [InlineData(724)]
    [InlineData(726)]
    [InlineData(999001)]
    public void ResolveCreditAmount_AnyOtherItemId_ReturnsNull(int itemId)
    {
        Assert.Null(GpTicketCatalog.ResolveCreditAmount(itemId));
    }
}
