using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.Guilds;
using Fenrir.Application.Game.Hosting.Progression;
using Fenrir.Application.Game.Hosting.World;
using Fenrir.Application.Game.Hosting.World.Monsters;
using Fenrir.Application.Game.Hosting.World.WorldState;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Fenrir.Data.World;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Hosting.Extensions;

/// <summary>
///     Every long-running background process for the GameServer: the write-behind/heartbeat/ticker hosts moved
///     here from Fenrir.Application.Game.Domain, plus the connection listener and boot-order guard moved here
///     from Fenrir.GameServer's Program.cs during the project split.
/// </summary>
public static class HostingServiceCollectionExtensions
{
    public static IServiceCollection AddGameHosting(this IServiceCollection services)
    {
        AddWorldState(services);
        AddZoneWar(services);
        AddMonsterBossRespawnTracking(services);

        services.AddHostedService<TowerWarWriteBehindHost>();

        // C08: guild buff reserve decay (BuffTime counts down over real time -- see GuildBuffDecayHost's remarks
        // for why this is a plain BackgroundService rather than an ISimulationSystem) and the RvR ranking-board
        // cache refresh (seeded once in Program.cs before ZoneConnectionHost starts accepting connections).
        services.AddHostedService<GuildBuffDecayHost>();
        services.AddHostedService<GuildRankingRefreshHost>();

        services.AddHostedService<ZoneTickHost>();
        services.AddHostedService<MonsterLootFlushHost>();

        // ProgressWriteBehindHost is a plain singleton, NOT its own BackgroundService/IWriteBehindFlusher --
        // see its own remarks for why a second, independently-timed drain of the SAME shared DirtyTracker<int>
        // would be unsafe. PositionWriteBehindHost is the sole owner of the one write-behind loop/flush signal
        // and calls into it for the Vitals/Progression side of every drained batch.
        services.AddSingleton<ProgressWriteBehindHost>();

        // Same "one instance, three registrations" pattern for a hosted service other code also needs to call directly.
        services.AddSingleton<PositionWriteBehindHost>();
        services.AddSingleton<IWriteBehindFlusher>(sp => sp.GetRequiredService<PositionWriteBehindHost>());
        services.AddHostedService(sp => sp.GetRequiredService<PositionWriteBehindHost>());

        services.AddHostedService<GameServerDirectoryHeartbeat>();
        services.AddHostedService<HeroRankingRolloverHost>();
        services.AddHostedService<GameConnectionHost>();

        // Cross-process duplicate-login kick/refusal, Game-side half -- see AccountSessionKickPollHost's remarks.
        services.AddHostedService<AccountSessionKickPollHost>();

        return services;
    }

    /// <summary>
    ///     Same "loader is resolved explicitly at boot" shape as <c>WorldDataServiceCollectionExtensions</c>:
    ///     Program.cs must still call <see cref="WorldStateService.InitializeAsync" /> before accepting
    ///     connections -- registering the singleton here does not load it.
    /// </summary>
    private static void AddWorldState(IServiceCollection services)
    {
        services.AddSingleton<IWorldStateRepository, WorldStateRepository>();
        services.AddSingleton<WorldStateService>();
        services.AddSingleton<WorldStateWriteBehindHost>();
        services.AddHostedService(static provider => provider.GetRequiredService<WorldStateWriteBehindHost>());
    }

    /// <summary>
    ///     Same "one instance, resolved directly by tests/tools as well as by the host" shape as
    ///     <see cref="AddWorldState" />.
    /// </summary>
    private static void AddZoneWar(IServiceCollection services)
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
    }

    /// <summary>
    ///     Same "loader is resolved explicitly at boot" shape as <see cref="AddWorldState" />: Program.cs must
    ///     still call <see cref="MonsterBossRespawnTracker.InitializeAsync" /> before <c>ZoneTickHost</c> starts
    ///     ticking -- registering the singleton here does not load it.
    /// </summary>
    private static void AddMonsterBossRespawnTracking(IServiceCollection services)
    {
        services.AddSingleton<IMonsterBossRespawnTimerRepository, MonsterBossRespawnTimerRepository>();
        services.AddSingleton<MonsterBossRespawnTracker>();
        services.AddHostedService<MonsterBossRespawnWriteBehindHost>();
    }
}
