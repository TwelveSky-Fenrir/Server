using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Domain.Game.GameData.Extensions;

public static class WorldDataServiceCollectionExtensions
{
    public static IServiceCollection AddWorldData(this IServiceCollection services)
    {
        services.AddSingleton<WorldDataLoader>();
        services.AddSingleton(static provider => provider.GetRequiredService<WorldDataLoader>().Cache);

        return services;
    }
}
