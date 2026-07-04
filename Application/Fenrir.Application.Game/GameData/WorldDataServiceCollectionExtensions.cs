using Fenrir.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.GameData;

/// <summary>
///     Registers the world.* reference-data surface: the boot-only <see cref="WorldDataRepository" /> (kept
///     here rather than in AddFenrirData because only the GameServer's loader ever consumes it), the
///     <see cref="WorldDataLoader" />, and the <see cref="WorldDataCache" /> itself -- resolved through the
///     loader so any consumer that fires before Program.cs awaited
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
