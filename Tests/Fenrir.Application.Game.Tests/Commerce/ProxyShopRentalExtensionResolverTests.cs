using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Commerce;

/// <summary>
///     Pure-logic coverage for <see cref="ProxyShopRentalExtensionResolver" /> -- the proxy-shop
///     rental-extension consumables (world.Items 567/592/8422/8423) handled by
///     <c>UseInventoryItemService</c>'s dispatch.
/// </summary>
public class ProxyShopRentalExtensionResolverTests
{
    [Theory]
    [InlineData(567, 1)]
    [InlineData(8422, 1)]
    [InlineData(592, 7)]
    [InlineData(8423, 7)]
    public void ExtensionDaysFor_RecognizedItems_ReturnsTheirFixedDayCount(int itemId, int expectedDays)
    {
        Assert.Equal(expectedDays, ProxyShopRentalExtensionResolver.ExtensionDaysFor(itemId));
    }

    [Fact]
    public void ExtensionDaysFor_UnrecognizedItem_ReturnsNull()
    {
        Assert.Null(ProxyShopRentalExtensionResolver.ExtensionDaysFor(999_001));
    }

    [Fact]
    public void Resolve_UnrecognizedItem_ReturnsNotRecognized()
    {
        var result = ProxyShopRentalExtensionResolver.Resolve(999_001, 20260706, 0);

        Assert.Equal(ProxyShopRentalExtensionResolver.Outcome.NotRecognized, result.Outcome);
        Assert.Equal(GameDate.Invalid, result.NewExpirationDate);
    }

    [Fact]
    public void Resolve_NoExistingExpiration_ExtendsFromToday()
    {
        // No game.OfflineShops row yet -- caller passes 0 for "never set".
        var result = ProxyShopRentalExtensionResolver.Resolve(567, 20260706, 0);

        Assert.Equal(ProxyShopRentalExtensionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(20260707, result.NewExpirationDate);
    }

    [Fact]
    public void Resolve_ExistingExpirationInThePast_ExtendsFromTodayWithNoCompounding()
    {
        var result = ProxyShopRentalExtensionResolver.Resolve(592, 20260706, 20260601);

        Assert.Equal(ProxyShopRentalExtensionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(20260713, result.NewExpirationDate);
    }

    [Fact]
    public void Resolve_ExistingExpirationExactlyToday_ExtendsFromTodayWithNoCompounding()
    {
        var result = ProxyShopRentalExtensionResolver.Resolve(567, 20260706, 20260706);

        Assert.Equal(ProxyShopRentalExtensionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(20260707, result.NewExpirationDate);
    }

    [Fact]
    public void Resolve_ExistingExpirationInTheFuture_CompoundsOntoTheRemainingTime()
    {
        // Existing expiration is 10 days out; a 7-day extension should land 7 days past THAT date, not today.
        var result = ProxyShopRentalExtensionResolver.Resolve(592, 20260706, 20260716);

        Assert.Equal(ProxyShopRentalExtensionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(20260723, result.NewExpirationDate);
    }

    [Fact]
    public void Resolve_CompoundingCrossesAMonthBoundary_ProjectsARealCalendarDate()
    {
        var result = ProxyShopRentalExtensionResolver.Resolve(567, 20260130, 20260131);

        Assert.Equal(ProxyShopRentalExtensionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(20260201, result.NewExpirationDate);
    }

    [Fact]
    public void Resolve_ExtremeExistingExpiration_ReturnsInvalidDateSentinel()
    {
        // The maximum representable calendar date -- projecting one more day forward overflows.
        var result = ProxyShopRentalExtensionResolver.Resolve(567, 20260706, 99991231);

        Assert.Equal(ProxyShopRentalExtensionResolver.Outcome.InvalidDate, result.Outcome);
        Assert.Equal(GameDate.Invalid, result.NewExpirationDate);
    }
}
