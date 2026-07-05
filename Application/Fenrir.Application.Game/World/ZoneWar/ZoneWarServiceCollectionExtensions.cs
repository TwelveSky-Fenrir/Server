using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.World.ZoneWar;

/// <summary>
///     Same "one instance, resolved directly by tests/tools as well as by the host" shape as
///     <c>WorldStateServiceCollectionExtensions</c>.
/// </summary>
public static class ZoneWarServiceCollectionExtensions
{
    public static IServiceCollection AddZoneWar(this IServiceCollection services)
    {
        services.AddSingleton<TribeVoteElection>();
        services.AddSingleton<ZoneEventBroadcaster>();

        // Registered as a factory (opaque to the DI container's constructor-graph cycle check) so that
        // MonsterSpawnScheduler -- an ISimulationSystem that ZoneRegistry itself resolves at construction
        // time -- can depend on ZoneEventBroadcaster without the container seeing a same-graph cycle back
        // through ZoneEventBroadcaster's own ZoneRegistry dependency. The factory closure only captures
        // the container; it does not resolve ZoneEventBroadcaster until something actually calls .Value,
        // by which point every singleton (including ZoneRegistry) is already constructed and cached.
        services.AddSingleton(sp => new Lazy<ZoneEventBroadcaster>(sp.GetRequiredService<ZoneEventBroadcaster>));

        services.AddSingleton<ZoneWarTickService>();
        services.AddHostedService(static provider => provider.GetRequiredService<ZoneWarTickService>());

        return services;
    }
}
