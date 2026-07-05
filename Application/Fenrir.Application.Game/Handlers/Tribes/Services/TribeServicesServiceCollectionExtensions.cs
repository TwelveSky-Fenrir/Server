using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.Tribes.Services;

/// <summary>
///     Registers the business-logic services extracted out of the Handlers/Tribes packet handlers. Every
///     service here is a stateless orchestrator over existing repositories/registries, so singleton lifetime
///     matches those dependencies (same reasoning as <c>WorldDataServiceCollectionExtensions</c>).
/// </summary>
public static class TribeServicesServiceCollectionExtensions
{
    public static IServiceCollection AddTribeServices(this IServiceCollection services)
    {
        services.AddSingleton<ITribeActionService, TribeActionService>();
        services.AddSingleton<ITribeAnnouncementScrollService, TribeAnnouncementScrollService>();
        services.AddSingleton<ITribeBankService, TribeBankService>();
        services.AddSingleton<ITribePopulationService, TribePopulationService>();
        services.AddSingleton<ITribeVoteService, TribeVoteService>();

        return services;
    }
}
