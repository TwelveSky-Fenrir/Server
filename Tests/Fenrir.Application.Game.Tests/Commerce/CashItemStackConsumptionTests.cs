using Fenrir.Application.Game.Domain.Commerce;

namespace Fenrir.Application.Game.Tests.Commerce;

/// <summary>
///     <see cref="CashItemStackConsumption" /> currently defaults every item id to whole-stack consumption
///     (see that type's own remarks on the unresolved stack-safe-category ambiguity for world.Items
///     567/592/8422/8423) -- these tests pin the current default so a future item-data-driven fix is a
///     visible, deliberate test change rather than a silent behavior drift.
/// </summary>
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
