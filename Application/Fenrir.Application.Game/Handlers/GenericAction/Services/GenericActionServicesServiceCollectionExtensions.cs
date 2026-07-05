using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.GenericAction.Services;

/// <summary>Registers the stateless orchestrator services backing <see cref="GenericActionHandler" />.</summary>
public static class GenericActionServicesServiceCollectionExtensions
{
    public static IServiceCollection AddGenericActionServices(this IServiceCollection services)
    {
        services.AddSingleton<IGenericActionService, GenericActionService>();

        return services;
    }
}
