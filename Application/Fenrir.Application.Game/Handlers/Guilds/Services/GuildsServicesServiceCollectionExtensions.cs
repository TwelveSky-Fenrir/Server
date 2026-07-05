using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.Guilds.Services;

/// <summary>Registers the Guilds handler batch's extracted business-logic services.</summary>
public static class GuildsServicesServiceCollectionExtensions
{
    public static IServiceCollection AddGuildsServices(this IServiceCollection services)
    {
        services.AddSingleton<IGuildActionService, GuildActionService>();

        return services;
    }
}
