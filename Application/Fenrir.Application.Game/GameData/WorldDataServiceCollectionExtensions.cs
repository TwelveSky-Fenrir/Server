using Fenrir.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.GameData;

/// <summary>
///     Cache is resolved through the loader so a consumer that fires before
///     <see cref="WorldDataLoader.InitializeAsync" /> fails loudly instead of reading an empty world.
/// </summary>
public static class WorldDataServiceCollectionExtensions
{
    public static IServiceCollection AddWorldData(this IServiceCollection services)
    {
        services.AddSingleton<IWorldDataRepository, WorldDataRepository>();
        services.AddSingleton<WorldDataLoader>();
        services.AddSingleton(static provider => provider.GetRequiredService<WorldDataLoader>().Cache);

        return services;
    }
}
