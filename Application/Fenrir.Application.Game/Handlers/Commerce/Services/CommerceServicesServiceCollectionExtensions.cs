using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>
///     Registers every Commerce-handler business-logic service extracted from
///     <c>Application/Fenrir.Application.Game/Handlers/Commerce</c>. Singleton, same lifetime reasoning as the
///     existing repositories: these are stateless orchestrators.
/// </summary>
public static class CommerceServicesServiceCollectionExtensions
{
    public static IServiceCollection AddCommerceServices(this IServiceCollection services)
    {
        services.AddSingleton<IBuyBloodMarkItemService, BuyBloodMarkItemService>();
        services.AddSingleton<IBuyCashItemService, BuyCashItemService>();
        services.AddSingleton<IBuyShopItemService, BuyShopItemService>();
        services.AddSingleton<IClaimDailyRewardService, ClaimDailyRewardService>();
        services.AddSingleton<ICloseShopStallService, CloseShopStallService>();
        services.AddSingleton<IGetBloodMarkCatalogService, GetBloodMarkCatalogService>();
        services.AddSingleton<IGetCashBalanceService, GetCashBalanceService>();
        services.AddSingleton<IGetCashCatalogService, GetCashCatalogService>();
        services.AddSingleton<IGetDailyRewardCatalogService, GetDailyRewardCatalogService>();
        services.AddSingleton<IGetProxyShopService, GetProxyShopService>();
        services.AddSingleton<IOpenShopStallService, OpenShopStallService>();
        services.AddSingleton<ISearchShopListingsService, SearchShopListingsService>();
        services.AddSingleton<IUpdateProxyShopService, UpdateProxyShopService>();
        services.AddSingleton<IViewShopStallService, ViewShopStallService>();
        services.AddSingleton<IWithdrawProxyShopEarningsService, WithdrawProxyShopEarningsService>();

        return services;
    }
}
