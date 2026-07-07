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
using Fenrir.Network.Dispatch;
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

// Hosted services only start inside host.Run(), so awaiting this here guarantees the world.* reference-data
// cache is populated before the first connection -- a SQL failure aborts startup instead of serving an empty world.
await host.Services.GetRequiredService<WorldDataLoader>().InitializeAsync(CancellationToken.None);

// Same rationale again: mirrors legacy MyGame::Init's own one-shot synchronous InitItemMall/InitBloodShop
// pass -- without this, the first CZ_GET_CASH_ITEM_INFO_SEND/CZ_DEMAND_BLOOD_MARK_SEND of the shard's life
// would see an empty catalog until CommerceCatalogRefreshHost's first periodic pass caught up.
await host.Services.GetRequiredService<CommerceCatalogCache>()
    .RefreshAllAsync(host.Services.GetRequiredService<IWorldDataRepository>(), CancellationToken.None);

// Same rationale: RvR world state (tribe symbols/points/gate/alliance offers) must be loaded before any
// zone actor or handler can read/mutate it.
await host.Services.GetRequiredService<WorldStateService>().InitializeAsync(CancellationToken.None);

// Same rationale again: without this, the first players in would broadcast an empty guild ranking board
// until GuildRankingRefreshHost's first periodic pass caught up.
await host.Services.GetRequiredService<GuildRankingCache>()
    .RefreshAsync(host.Services.GetRequiredService<IGuildRepository>(), CancellationToken.None);

// Same rationale again: a tower's guardian-monster lifecycle must resume from game.TowerState before
// TowerGuardianSystem's first tick, instead of starting every tower at Dormant every restart.
await host.Services.GetRequiredService<TowerWarState>()
    .InitializeAsync(host.Services.GetRequiredService<ITowerRepository>(), CancellationToken.None);

// Same rationale again: the persisted "Yanggok" named-boss respawn deadlines (monsters 564-568) must be in
// memory before MonsterSpawnScheduler's first tick for any zone, or a freshly booted server would pop them
// back in immediately regardless of how recently they were killed.
await host.Services.GetRequiredService<MonsterBossRespawnTracker>()
    .InitializeAsync(host.Services.GetRequiredService<IMonsterBossRespawnTimerRepository>(), CancellationToken.None);

// Must run before ZoneTickHost/ZoneConnectionHost start accepting ticks or connections.
var shardId = host.Services.GetRequiredService<IOptions<GameServerOptions>>().Value.ShardId;
var hostedMaps = await host.Services.GetRequiredService<IShardMapAssignmentRepository>()
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
await ShardPartitionGuard.EnsureNoOverlapAsync(shardId, hostedMaps,
    host.Services.GetRequiredService<IGameServerDirectoryRepository>(),
    host.Services.GetRequiredService<IShardMapAssignmentRepository>(),
    CancellationToken.None);

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

var bootLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Fenrir.GameServer.Boot");
foreach (var gap in unclaimedDesignatedMaps)
    bootLogger.LogWarning(
        "No live shard currently hosts map {MapId}, the designated map for {SchedulerName} -- that scheduler " +
        "is inert cluster-wide until admin.ShardMapAssignments assigns map {MapId} to some shard.",
        gap.MapId, gap.SchedulerName, gap.MapId);

host.Services.GetRequiredService<ZoneRegistry>().Initialize(hostedMaps);

await host.RunAsync();
