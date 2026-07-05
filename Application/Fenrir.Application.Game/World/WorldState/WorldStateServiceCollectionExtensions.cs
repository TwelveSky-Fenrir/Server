using Fenrir.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.World.WorldState;

/// <summary>
///     Same "loader is resolved explicitly at boot" shape as <c>WorldDataServiceCollectionExtensions</c>:
///     Program.cs must still call <see cref="WorldStateService.InitializeAsync" /> before accepting
///     connections -- registering the singleton here does not load it.
/// </summary>
public static class WorldStateServiceCollectionExtensions
{
    public static IServiceCollection AddWorldState(this IServiceCollection services)
    {
        services.AddSingleton<IWorldStateRepository, WorldStateRepository>();
        services.AddSingleton<WorldStateService>();
        services.AddSingleton<WorldStateWriteBehindHost>();
        services.AddHostedService(static provider => provider.GetRequiredService<WorldStateWriteBehindHost>());

        return services;
    }
}
