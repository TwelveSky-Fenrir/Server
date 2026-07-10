// SECURITY GUARDRAIL -- read before adding any HTTP/gRPC surface to this executable.
// Legacy ts25center bound an unauthenticated cpp-httplib dashboard on 127.0.0.1:2499 with zero
// authentication anywhere in its routing (no set_pre_routing_handler check) and a side-effecting
// GET /Shutdown that armed a cluster-wide kill switch (Server/ts25center/S02_MyServer.cpp:56-82,215-253;
// ServerDocs/10_ts25center/03_HTTP_Dashboard_NonAuth_CRITIQUE.md; ServerDocs/04_SECURITE_ET_DETTE_TECHNIQUE.md
// finding #2). This is the single worst legacy anti-pattern this project's security audits have found.
// Fenrir.GameServer is deliberately a Microsoft.NET.Sdk.Worker project with no ASP.NET Core reference at
// all (Host.CreateApplicationBuilder, never WebApplication -- see Orchestration/Fenrir.ServiceDefaults'
// own "TCP-socket-only servers" comments), and Tests/Fenrir.IntegrationTests/NoUnauthenticatedHttpSurfaceTests.cs
// regression-tests that this stays true. The day anyone proposes an HTTP/gRPC admin, metrics, shard-control,
// or GM surface on this process: it must get its own explicit authentication and authorization from line
// one, must never be assumed to inherit ClientSession/game-session auth, must never expose a state-mutating
// action behind an unauthenticated GET (or any verb without a real credential check), and must bind to a
// scope no broader than the ts25 loopback-only precedent unless there is an explicit, reviewed reason to
// widen it.

using Fenrir.Network.Dispatch;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Extensions;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.GameData.Extensions;
using Fenrir.Application.Game.Handlers.Extensions;
using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Hosting.Extensions;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Fenrir.Application.Game.Services.Extensions;
using Fenrir.Data;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Progression;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.ServiceDefaults;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddFenrirData();

builder.Services.Configure<GameServerOptions>(builder.Configuration.GetSection("Game"));
builder.Services.AddGameDomain();
builder.Services.AddWorldData();
builder.Services.AddGameServices();
builder.Services.AddGameHosting();
builder.Services.AddGameHandlers();

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<ISessionRateLimiter, SessionRateLimiter>();

var host = builder.Build();

// Must run before ZoneConnectionHost starts accepting connections: MessageDispatcher resolves handlers through this provider.
PacketHandlerHub.Initialize(host.Services);

var bootLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Fenrir.GameServer.Boot");

// Everything below is sequential, one-shot boot-time initialization -- unlike a BackgroundService's own tick
// loop, a failure here must still crash the process (fail fast, never serve on a half-populated cache), but
// until this wrapper existed, a failure here (e.g. the WorldStateService.InitializeAsync PK-violation-race
// production incident this was added for) produced ONLY a raw unhandled-exception dump to stderr, with no
// application-level trace of which step was in flight. bootStep is updated immediately before each await so
// the single catch below can name the failing step in one LogCritical line before rethrowing -- the process
// still exits non-zero exactly as before, this only makes the failure diagnosable from logs alone.
var bootStep = "(unknown)";
byte shardId;
IReadOnlyList<short> hostedMaps;

try
{
    // Hosted services only start inside host.Run(), so awaiting this here guarantees the world.* reference-data
    // cache is populated before the first connection -- a SQL failure aborts startup instead of serving an empty world.
    bootStep = "WorldDataLoader.InitializeAsync";
    await host.Services.GetRequiredService<WorldDataLoader>().InitializeAsync(CancellationToken.None);

    // Same rationale again: mirrors legacy MyGame::Init's own one-shot synchronous InitItemMall/InitBloodShop
    // pass -- without this, the first CZ_GET_CASH_ITEM_INFO_SEND/CZ_DEMAND_BLOOD_MARK_SEND of the shard's life
    // would see an empty catalog until CommerceCatalogRefreshHost's first periodic pass caught up.
    bootStep = "CommerceCatalogCache.RefreshAllAsync";
    await host.Services.GetRequiredService<CommerceCatalogCache>()
        .RefreshAllAsync(host.Services.GetRequiredService<IWorldDataRepository>(), CancellationToken.None);

    // Same rationale: RvR world state (tribe symbols/points/gate/alliance offers) must be loaded before any
    // zone actor or handler can read/mutate it.
    bootStep = "WorldStateService.InitializeAsync";
    await host.Services.GetRequiredService<WorldStateService>().InitializeAsync(CancellationToken.None);

    // Same rationale again: without this, the first players in would broadcast an empty guild ranking board
    // until GuildRankingRefreshHost's first periodic pass caught up.
    bootStep = "GuildRankingCache.RefreshAsync";
    await host.Services.GetRequiredService<GuildRankingCache>()
        .RefreshAsync(host.Services.GetRequiredService<IGuildRepository>(), CancellationToken.None);

    // Same rationale again: a tower's guardian-monster lifecycle must resume from game.TowerState before
    // TowerGuardianSystem's first tick, instead of starting every tower at Dormant every restart.
    bootStep = "TowerWarState.InitializeAsync";
    await host.Services.GetRequiredService<TowerWarState>()
        .InitializeAsync(host.Services.GetRequiredService<ITowerRepository>(), CancellationToken.None);

    // Same rationale again: the persisted "Yanggok" named-boss respawn deadlines (monsters 564-568) must be in
    // memory before MonsterSpawnScheduler's first tick for any zone, or a freshly booted server would pop them
    // back in immediately regardless of how recently they were killed.
    bootStep = "MonsterBossRespawnTracker.InitializeAsync";
    await host.Services.GetRequiredService<MonsterBossRespawnTracker>()
        .InitializeAsync(host.Services.GetRequiredService<IMonsterBossRespawnTimerRepository>(),
            CancellationToken.None);

    // Must run before ZoneTickHost/ZoneConnectionHost start accepting ticks or connections.
    bootStep = "IShardMapAssignmentRepository.GetHostedMapsAsync";
    shardId = host.Services.GetRequiredService<IOptions<GameServerOptions>>().Value.ShardId;
    hostedMaps = await host.Services.GetRequiredService<IShardMapAssignmentRepository>()
        .GetHostedMapsAsync(shardId, CancellationToken.None);

    if (hostedMaps.Count == 0)
        throw new InvalidOperationException(
            $"No maps assigned to shard {shardId} in admin.ShardMapAssignments -- a GameServer hosting no world is always a configuration mistake.");

    // ADR-0012 rule 1: a shard is a disjoint map partition, never a replica. Must run before ZoneRegistry.Initialize
    // so a colliding shard fails fast at boot instead of silently duplicating a Zone another shard already hosts --
    // "another shard" here means either a currently-live one (cross-checked via runtime.GameServerDirectory) or one
    // merely configured in admin.ShardMapAssignments but not yet live, which matters because this shard's own
    // heartbeat (below, inside host.RunAsync()) has not started yet either: on a whole fleet cold-booting together,
    // no shard would otherwise be visible to any other shard's check at this exact moment.
    bootStep = "ShardPartitionGuard.EnsureNoOverlapAsync";
    await ShardPartitionGuard.EnsureNoOverlapAsync(shardId, hostedMaps,
        host.Services.GetRequiredService<IGameServerDirectoryRepository>(),
        host.Services.GetRequiredService<IShardMapAssignmentRepository>(),
        CancellationToken.None);
}
catch (Exception ex)
{
    bootLogger.LogCritical(ex,
        "Fenrir.GameServer boot failed during step '{BootStep}' -- process will exit without accepting connections",
        bootStep);
    throw;
}

// Complements the guard above: not "do two shards collide" (impossible to reach this line if so) but
// "is any live shard actually hosting the map each singleton RvR scheduler is configured to run on".
// Service-degradation risk (an inert scheduler), not data-corruption -- logged, never fatal.
var gameOptions = host.Services.GetRequiredService<IOptions<GameServerOptions>>().Value;
var unclaimedDesignatedMaps = await SingletonRvrSchedulerGuard.FindUnclaimedDesignatedMapsAsync(
    [
        new SingletonRvrSchedulerValidator.DesignatedMapClaim(nameof(TribeVoteElectionCalendarHost),
            gameOptions.VoteTribeMapId),
        new SingletonRvrSchedulerValidator.DesignatedMapClaim(nameof(TribeSymbolBattleSchedulerHost),
            gameOptions.TribeSymbolBattleMapId),
        new SingletonRvrSchedulerValidator.DesignatedMapClaim(nameof(HolyStoneWarCycleHost),
            gameOptions.HolyStoneMapId),
        new SingletonRvrSchedulerValidator.DesignatedMapClaim(nameof(AllianceDiplomacyCeremonyHost),
            gameOptions.AllianceTribeMapId)
    ],
    host.Services.GetRequiredService<IGameServerDirectoryRepository>(),
    host.Services.GetRequiredService<IShardMapAssignmentRepository>(),
    CancellationToken.None);

foreach (var gap in unclaimedDesignatedMaps)
    bootLogger.LogWarning(
        "No live shard currently hosts map {MapId}, the designated map for {SchedulerName} -- that scheduler " +
        "is inert cluster-wide until admin.ShardMapAssignments assigns map {MapId} to some shard.",
        gap.MapId, gap.SchedulerName, gap.MapId);

var zoneRegistry = host.Services.GetRequiredService<ZoneRegistry>();
zoneRegistry.Initialize(hostedMaps);

// Aggregates the per-zone LogWarning (missing file)/LogError (parse failure) Zone.TryLoadGeometry already
// emitted once per Zone above, inside ZoneRegistry.Initialize, into one glanceable rollup -- same "collect,
// then log one summary via the Boot logger" shape as unclaimedDesignatedMaps above. Pure observability: does
// not change Zone's own degrade-to-speed-only-validation behavior (see GameServerOptionsValidator's remarks
// for why a missing/unparseable .WM is deliberately not startup-blocking), it only makes "which of this
// shard's hosted maps are currently running with degraded (speed-only, unconstrained-pathing) anti-cheat"
// visible at a glance instead of requiring an operator to scroll back through N individual per-zone log lines.
var mapsMissingGeometry = new List<short>();
foreach (var zone in zoneRegistry.Zones)
    if (zone.Geometry is null)
        mapsMissingGeometry.Add(zone.MapId);

if (mapsMissingGeometry.Count > 0)
    bootLogger.LogWarning(
        "{MissingCount} of {HostedCount} hosted map(s) loaded without navmesh (.WM) data: [{MapIds}] -- on these " +
        "maps, monster pathing (MonsterAiSystem.MoveToward) is unconstrained by terrain/obstacles and " +
        "player-movement validation (MovementRules.IsPlausible) degrades to its per-move distance check only, until real " +
        ".WM assets are deployed for them. See the per-map warning/error already logged above by " +
        "Zone.TryLoadGeometry for each map's specific cause (missing file vs. parse failure).",
        mapsMissingGeometry.Count, hostedMaps.Count, string.Join(", ", mapsMissingGeometry));

await host.RunAsync();
