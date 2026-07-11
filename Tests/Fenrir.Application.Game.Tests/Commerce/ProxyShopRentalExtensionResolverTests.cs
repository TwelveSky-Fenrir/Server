using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Commerce;

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
        var result = ProxyShopRentalExtensionResolver.Resolve(567, 20260706, 99991231);

        Assert.Equal(ProxyShopRentalExtensionResolver.Outcome.InvalidDate, result.Outcome);
        Assert.Equal(GameDate.Invalid, result.NewExpirationDate);
    }
}
