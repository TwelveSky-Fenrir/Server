using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Commerce;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Commerce;

/// <summary>
///     Covers <see cref="GetCashCatalogService" />: it always answers with the live
///     <see cref="CommerceCatalogCache" /> contents/version, and records
///     <see cref="PlayerRuntimeState.KnownCashCatalogVersion" /> for the resolving session so
///     <see cref="Fenrir.Application.Game.Domain.Simulation.CashCatalogStaleNotifySystem" />'s own bookkeeping
///     stays correct.
/// </summary>
public class GetCashCatalogServiceTests
{
    private static async Task<CommerceCatalogCache> CacheAtVersionAsync(int version)
    {
        var cache = new CommerceCatalogCache(NullLogger<CommerceCatalogCache>.Instance);
        var repository = new FakeWorldDataRepository
        {
            ItemMallProducts = [new ItemMallProductRowDto(100000, 5, version, 0, 0, true)]
        };
        await cache.RefreshCashCatalogAsync(repository, CancellationToken.None);
        return cache;
    }

    [Fact]
    public async Task GetCatalog_ReturnsTheLiveCacheVersionAndDisplayGrid()
    {
        var cache = await CacheAtVersionAsync(9);
        var service = new GetCashCatalogService(cache, NullLogger<GetCashCatalogService>.Instance);

        var response = service.GetCatalog(null);

        Assert.Equal(0, response.Result);
        Assert.Equal(9, response.Version);
        Assert.Equal(cache.CashCatalog.DisplayGrid, response.CashItemInfo);
    }

    [Fact]
    public async Task GetCatalog_WithAResolvedPlayer_RecordsTheReturnedVersionOntoTheSession()
    {
        var cache = await CacheAtVersionAsync(9);
        var service = new GetCashCatalogService(cache, NullLogger<GetCashCatalogService>.Instance);
        var (zone, state) = SetUpPlayer();

        service.GetCatalog(state);

        Assert.Equal(9, state.KnownCashCatalogVersion);
        _ = zone; // keep the zone alive for the state's own Session reference
    }

    [Fact]
    public async Task GetCatalog_WithNoResolvedPlayer_StillReturnsTheCatalog_WithoutThrowing()
    {
        var cache = await CacheAtVersionAsync(9);
        var service = new GetCashCatalogService(cache, NullLogger<GetCashCatalogService>.Instance);

        var response = service.GetCatalog(null);

        Assert.Equal(9, response.Version);
    }

    private static (Zone Zone, PlayerRuntimeState State) SetUpPlayer()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!);
    }
}
