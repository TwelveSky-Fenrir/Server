using Fenrir.Application.Game;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Dispatching;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Social.Duel;
using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.Social.Mentor;
using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Monsters;
using Fenrir.Application.Game.World.WorldState;
using Fenrir.Application.Game.World.ZoneWar;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch;
using Fenrir.Data;
using Fenrir.Data.Admin;
using Fenrir.Data.Guilds;
using Fenrir.Data.Progression;
using Fenrir.Data.Runtime;
using Fenrir.Data.World;
using Fenrir.Data.WriteBehind;
using Fenrir.GameServer;
using Fenrir.Network.RateLimiting;
using Fenrir.Network.Sessions;
using Fenrir.ServiceDefaults;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddFenrirData();

builder.Services.Configure<GameServerOptions>(builder.Configuration.GetSection("Game"));
builder.Services.AddSingleton<IValidateOptions<GameServerOptions>, GameServerOptionsValidator>();
builder.Services.AddOptions<GameServerOptions>().ValidateOnStart();
builder.Services.AddGameHandlers();
builder.Services.AddWorldData();
builder.Services.AddWorldState();
builder.Services.AddZoneWar();
builder.Services.AddMonsterBossRespawnTracking();

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<ISessionRateLimiter, SessionRateLimiter>();
builder.Services.AddSingleton<IFrameDispatcher, ZoneFrameDispatcher>();

builder.Services.AddSingleton<MovementRules>();
builder.Services.AddSingleton<DirtyTracker<int>>();

builder.Services.AddSingleton<QuestCatalog>();
builder.Services.AddSingleton<KillCooldownTracker>(); // C05 anti-farm gate, shared by every Zone via ZoneRegistry

// Registration order IS simulation order within a zone's tick: buffs must expire before meditation regen reads
// a (possibly just-cleared) sit-skill, and before auto-hunt decides which configured buff is still active;
// monster AI runs before that tick's respawn scan.
builder.Services.AddSingleton<ISimulationSystem, BuffExpirySystem>();
builder.Services.AddSingleton<ISimulationSystem, AutoHuntTickSystem>();
builder.Services.AddSingleton<ISimulationSystem, MeditationRegenSystem>();
builder.Services.AddSingleton<ISimulationSystem, MonsterAiSystem>();
builder.Services.AddSingleton<ISimulationSystem, MonsterSpawnScheduler>();
builder.Services.AddSingleton<ISimulationSystem, TowerGuardianSystem>();
builder.Services.AddSingleton<ISimulationSystem, PetActivitySystem>();

builder.Services.AddSingleton<ZoneRegistry>();

// Process-wide singletons: a party/duel/trade/friend-ask/mentor-ask negotiation can span multiple Zone actors.
builder.Services.AddSingleton<PartyRegistry>();
builder.Services.AddSingleton<FriendRegistry>();
builder.Services.AddSingleton<MentorRegistry>();
builder.Services.AddSingleton<DuelRegistry>();
builder.Services.AddSingleton<TradeRegistry>();
builder.Services.AddSingleton<GuildInviteRegistry>();
builder.Services.AddSingleton<TowerWarState>();
builder.Services.AddHostedService<TowerWarWriteBehindHost>();

// C08: guild buff reserve decay (BuffTime counts down over real time -- see GuildBuffDecayHost's remarks for
// why this is a plain BackgroundService rather than an ISimulationSystem) and the RvR ranking-board cache
// (GuildRankingCache.Top is read synchronously by EnterWorldHandler/ZoneMoveHandler; kept warm by a periodic
// refresh, seeded once below before ZoneConnectionHost starts accepting connections).
builder.Services.AddSingleton<GuildRankingCache>();
builder.Services.AddHostedService<GuildBuffDecayHost>();
builder.Services.AddHostedService<GuildRankingRefreshHost>();

builder.Services.AddHostedService<ZoneTickHost>();
builder.Services.AddHostedService<MonsterLootFlushHost>();

// Same "one instance, three registrations" pattern for a hosted service other code also needs to call directly.
builder.Services.AddSingleton<PositionWriteBehindHost>();
builder.Services.AddSingleton<IWriteBehindFlusher>(sp => sp.GetRequiredService<PositionWriteBehindHost>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<PositionWriteBehindHost>());

builder.Services.AddHostedService<GameServerDirectoryHeartbeat>();
builder.Services.AddHostedService<HeroRankingRolloverHost>();
builder.Services.AddHostedService<ZoneConnectionHost>();

var host = builder.Build();

// Must run before ZoneConnectionHost starts accepting connections: MessageDispatcher resolves handlers through this provider.
PacketHandlerHub.Initialize(host.Services);

// Hosted services only start inside host.Run(), so awaiting this here guarantees the world.* reference-data
// cache is populated before the first connection -- a SQL failure aborts startup instead of serving an empty world.
await host.Services.GetRequiredService<WorldDataLoader>().InitializeAsync(CancellationToken.None);

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
// so a colliding shard fails fast at boot instead of silently duplicating a Zone another live shard already hosts.
await ShardPartitionGuard.EnsureNoOverlapAsync(shardId, hostedMaps,
    host.Services.GetRequiredService<IGameServerDirectoryRepository>(),
    host.Services.GetRequiredService<IShardMapAssignmentRepository>(),
    CancellationToken.None);

host.Services.GetRequiredService<ZoneRegistry>().Initialize(hostedMaps);

await host.RunAsync();
