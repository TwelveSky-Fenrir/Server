using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.Progression.Services;

/// <summary>
///     Registers the stateless orchestrator services extracted out of the Progression handlers -- same
///     singleton-lifetime reasoning as the existing repositories these wrap.
/// </summary>
public static class ProgressionServicesServiceCollectionExtensions
{
    public static IServiceCollection AddProgressionServices(this IServiceCollection services)
    {
        services.AddSingleton<IAutoHuntToggleService, AutoHuntToggleService>();
        services.AddSingleton<IAutoPotionThresholdService, AutoPotionThresholdService>();
        services.AddSingleton<IDailyMissionService, DailyMissionService>();
        services.AddSingleton<IHeroRankingService, HeroRankingService>();
        services.AddSingleton<IHeroRewardClaimService, HeroRewardClaimService>();
        services.AddSingleton<ITowerUpgradeService, TowerUpgradeService>();

        return services;
    }
}
