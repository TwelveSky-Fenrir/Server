using Fenrir.Application.Game.Domain.Commerce;

namespace Fenrir.Application.Game.Tests.Commerce;

public class CashItemStackConsumptionTests
{
    [Theory]
    [InlineData(567)]
    [InlineData(592)]
    [InlineData(8422)]
    [InlineData(8423)]
    public void IsStackSafe_ProxyShopRentalExtensionItems_DefaultsToFalse(int itemId)
    {
        Assert.False(CashItemStackConsumption.IsStackSafe(itemId));
    }

    [Fact]
    public void RemainingQuantity_NotStackSafe_ZeroesTheWholeStackRegardlessOfCount()
    {
        Assert.Equal(0, CashItemStackConsumption.RemainingQuantity(567, 1));
        Assert.Equal(0, CashItemStackConsumption.RemainingQuantity(567, 5));
    }
}
