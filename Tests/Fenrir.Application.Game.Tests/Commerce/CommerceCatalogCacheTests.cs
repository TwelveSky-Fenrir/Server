using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Commerce;

public class CommerceCatalogCacheTests
{
    private static ItemMallProductRowDto Product(int id, byte type, int itemId, int quantity, int cost, bool active)
    {
        return new ItemMallProductRowDto(id, type, itemId, quantity, cost, active);
    }

    private static CommerceCatalogCache NewCache()
    {
        return new CommerceCatalogCache(NullLogger<CommerceCatalogCache>.Instance);
    }

    [Fact]
    public void BeforeAnyRefresh_StartsAtTheZeroedSentinelState()
    {
        var cache = NewCache();

        Assert.Equal(CommerceCatalogCache.InitialVersion, cache.CashCatalogVersion);
        Assert.Equal(CommerceCatalogCache.InitialVersion, cache.CashCatalogCrc);
        Assert.Equal(CommerceCatalogCache.InitialVersion, cache.BloodCatalogVersion);
        Assert.Empty(cache.BloodExchangeCatalog);
        Assert.All(cache.CashCatalog.DisplayGrid, v => Assert.Equal(-1, v));
    }

    [Fact]
    public async Task RefreshCashCatalogAsync_NoSentinelRowYet_VersionStaysAtZero_NoReloadAttempted()
    {
        var cache = NewCache();
        var repository = new FakeWorldDataRepository
        {
            ItemMallProducts = [Product(1, 1, 100, 0, 20, true)]
        };

        await cache.RefreshCashCatalogAsync(repository, CancellationToken.None);

        Assert.Equal(0, cache.CashCatalogVersion);
        Assert.False(cache.CashCatalog.CostInfoByIndex[0].IsAssigned);
    }

    [Fact]
    public async Task RefreshCashCatalogAsync_VersionChanged_SwapsCatalogAndVersionAndCrcTogether()
    {
        var cache = NewCache();
        var repository = new FakeWorldDataRepository
        {
            ItemMallProducts =
            [
                Product(1, 1, 100, 0, 20, true),
                Product(100000, 5, 7, 0, 0, true),
                Product(100001, 5, 42, 0, 0, true)
            ]
        };

        await cache.RefreshCashCatalogAsync(repository, CancellationToken.None);

        Assert.Equal(7, cache.CashCatalogVersion);
        Assert.Equal(42, cache.CashCatalogCrc);
        Assert.True(cache.CashCatalog.CostInfoByIndex[0].IsAssigned);
        Assert.Equal(100, cache.CashCatalog.CostInfoByIndex[0].ItemId);
    }

    [Fact]
    public async Task RefreshCashCatalogAsync_SameVersionAsBefore_LeavesCrcAndCatalogUntouched()
    {
        var cache = NewCache();
        var repository = new FakeWorldDataRepository
        {
            ItemMallProducts =
            [
                Product(1, 1, 100, 0, 20, true),
                Product(100000, 5, 7, 0, 0, true),
                Product(100001, 5, 42, 0, 0, true)
            ]
        };
        await cache.RefreshCashCatalogAsync(repository, CancellationToken.None);

        repository.ItemMallProducts =
        [
            Product(1, 1, 100, 0, 20, true),
            Product(100000, 5, 7, 0, 0, true),
            Product(100001, 5, 999, 0, 0, true)
        ];
        await cache.RefreshCashCatalogAsync(repository, CancellationToken.None);

        Assert.Equal(7, cache.CashCatalogVersion);
        Assert.Equal(42, cache.CashCatalogCrc);
    }

    [Fact]
    public async Task RefreshCashCatalogAsync_QueryFails_LeavesPreviousCatalogVersionAndCrcInPlace()
    {
        var cache = NewCache();
        var repository = new FakeWorldDataRepository
        {
            ItemMallProducts = [Product(100000, 5, 7, 0, 0, true), Product(100001, 5, 42, 0, 0, true)]
        };
        await cache.RefreshCashCatalogAsync(repository, CancellationToken.None);

        repository.ThrowOnGetItemMallProducts = true;
        await cache.RefreshCashCatalogAsync(repository, CancellationToken.None);

        Assert.Equal(7, cache.CashCatalogVersion);
        Assert.Equal(42, cache.CashCatalogCrc);
    }

    [Fact]
    public async Task RefreshBloodCatalogAsync_VersionChanged_SwapsRowsAndVersionTogether()
    {
        var cache = NewCache();
        var repository = new FakeWorldDataRepository
        {
            BloodExchangeCatalog =
            [
                new BloodExchangeCatalogRowDto(1, 12, 5, 0),
                new BloodExchangeCatalogRowDto(100000, 6, 0, 0)
            ]
        };

        await cache.RefreshBloodCatalogAsync(repository, CancellationToken.None);

        Assert.Equal(6, cache.BloodCatalogVersion);
        Assert.Equal(2, cache.BloodExchangeCatalog.Length);
    }

    [Fact]
    public async Task RefreshBloodCatalogAsync_QueryFails_LeavesPreviousRowsAndVersionInPlace()
    {
        var cache = NewCache();
        var repository = new FakeWorldDataRepository
        {
            BloodExchangeCatalog = [new BloodExchangeCatalogRowDto(100000, 6, 0, 0)]
        };
        await cache.RefreshBloodCatalogAsync(repository, CancellationToken.None);

        repository.ThrowOnGetBloodExchangeCatalog = true;
        await cache.RefreshBloodCatalogAsync(repository, CancellationToken.None);

        Assert.Equal(6, cache.BloodCatalogVersion);
        Assert.Single(cache.BloodExchangeCatalog);
    }

    [Fact]
    public async Task RefreshAllAsync_CashFailing_StillLetsBloodReloadIndependently()
    {
        var cache = NewCache();
        var repository = new FakeWorldDataRepository
        {
            ThrowOnGetItemMallProducts = true,
            BloodExchangeCatalog = [new BloodExchangeCatalogRowDto(100000, 6, 0, 0)]
        };

        await cache.RefreshAllAsync(repository, CancellationToken.None);

        Assert.Equal(CommerceCatalogCache.InitialVersion, cache.CashCatalogVersion);
        Assert.Equal(6, cache.BloodCatalogVersion);
    }

    [Fact]
    public async Task RefreshAllAsync_BloodFailing_StillLetsCashReloadIndependently()
    {
        var cache = NewCache();
        var repository = new FakeWorldDataRepository
        {
            ThrowOnGetBloodExchangeCatalog = true,
            ItemMallProducts = [Product(100000, 5, 7, 0, 0, true)]
        };

        await cache.RefreshAllAsync(repository, CancellationToken.None);

        Assert.Equal(7, cache.CashCatalogVersion);
        Assert.Equal(CommerceCatalogCache.InitialVersion, cache.BloodCatalogVersion);
    }
}
