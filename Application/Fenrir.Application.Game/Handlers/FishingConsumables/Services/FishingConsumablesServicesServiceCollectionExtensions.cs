using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.FishingConsumables.Services;

/// <summary>
///     Registers the business-logic services extracted from the fishing/bottle-consumable handlers
///     (<c>FishingCatchHandler</c>, <c>FishingLineHandler</c>, <c>FishingProgressHandler</c>,
///     <c>DrinkBottleHandler</c>). Stateless orchestrators, same singleton lifetime reasoning as the
///     repositories/registries they call into.
/// </summary>
public static class FishingConsumablesServicesServiceCollectionExtensions
{
    public static IServiceCollection AddFishingConsumablesServices(this IServiceCollection services)
    {
        services.AddSingleton<IFishingCatchService, FishingCatchService>();
        services.AddSingleton<IFishingLineService, FishingLineService>();
        services.AddSingleton<IFishingProgressService, FishingProgressService>();
        services.AddSingleton<IDrinkBottleService, DrinkBottleService>();

        return services;
    }
}
