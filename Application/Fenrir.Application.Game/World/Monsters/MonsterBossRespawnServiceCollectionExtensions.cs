using Fenrir.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     Same "loader is resolved explicitly at boot" shape as <c>WorldStateServiceCollectionExtensions</c>:
///     Program.cs must still call <see cref="MonsterBossRespawnTracker.InitializeAsync" /> before
///     <c>ZoneTickHost</c> starts ticking -- registering the singleton here does not load it.
/// </summary>
public static class MonsterBossRespawnServiceCollectionExtensions
{
    public static IServiceCollection AddMonsterBossRespawnTracking(this IServiceCollection services)
    {
        services.AddSingleton<IMonsterBossRespawnTimerRepository, MonsterBossRespawnTimerRepository>();
        services.AddSingleton<MonsterBossRespawnTracker>();
        services.AddHostedService<MonsterBossRespawnWriteBehindHost>();

        return services;
    }
}
