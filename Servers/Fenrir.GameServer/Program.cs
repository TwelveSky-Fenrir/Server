using Fenrir.Application.Game;
using Fenrir.Application.Game.Dispatching;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Social.Duel;
using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.Social.Mentor;
using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Monsters;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Dispatch;
using Fenrir.Data;
using Fenrir.Data.Admin;
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

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<ISessionRateLimiter, SessionRateLimiter>();
builder.Services.AddSingleton<IFrameDispatcher, ZoneFrameDispatcher>();

builder.Services.AddSingleton<MovementRules>();
builder.Services.AddSingleton<DirtyTracker<int>>();

builder.Services.AddSingleton<QuestCatalog>();

// Registration order IS simulation order within a zone's tick: buffs must expire before meditation regen reads
// a (possibly just-cleared) sit-skill; monster AI runs before that tick's respawn scan.
builder.Services.AddSingleton<ISimulationSystem, BuffExpirySystem>();
builder.Services.AddSingleton<ISimulationSystem, MeditationRegenSystem>();
builder.Services.AddSingleton<ISimulationSystem, MonsterAiSystem>();
builder.Services.AddSingleton<ISimulationSystem, MonsterSpawnScheduler>();
builder.Services.AddSingleton<ISimulationSystem, PetActivitySystem>();

builder.Services.AddSingleton<ZoneRegistry>();

// Process-wide singletons: a party/duel/trade/friend-ask/mentor-ask negotiation can span multiple Zone actors.
builder.Services.AddSingleton<PartyRegistry>();
builder.Services.AddSingleton<FriendRegistry>();
builder.Services.AddSingleton<MentorRegistry>();
builder.Services.AddSingleton<DuelRegistry>();
builder.Services.AddSingleton<TradeRegistry>();

builder.Services.AddHostedService<ZoneTickHost>();
builder.Services.AddHostedService<MonsterLootFlushHost>();

// Same "one instance, three registrations" pattern for a hosted service other code also needs to call directly.
builder.Services.AddSingleton<PositionWriteBehindHost>();
builder.Services.AddSingleton<IWriteBehindFlusher>(sp => sp.GetRequiredService<PositionWriteBehindHost>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<PositionWriteBehindHost>());

builder.Services.AddHostedService<GameServerDirectoryHeartbeat>();
builder.Services.AddHostedService<ZoneConnectionHost>();

var host = builder.Build();

// Must run before ZoneConnectionHost starts accepting connections: MessageDispatcher resolves handlers through this provider.
PacketHandlerHub.Initialize(host.Services);

// Hosted services only start inside host.Run(), so awaiting this here guarantees the world.* reference-data
// cache is populated before the first connection -- a SQL failure aborts startup instead of serving an empty world.
await host.Services.GetRequiredService<WorldDataLoader>().InitializeAsync(CancellationToken.None);

// Must run before ZoneTickHost/ZoneConnectionHost start accepting ticks or connections.
var shardId = host.Services.GetRequiredService<IOptions<GameServerOptions>>().Value.ShardId;
var hostedMaps = await host.Services.GetRequiredService<IShardMapAssignmentRepository>()
    .GetHostedMapsAsync(shardId, CancellationToken.None);

if (hostedMaps.Count == 0)
    throw new InvalidOperationException(
        $"No maps assigned to shard {shardId} in admin.ShardMapAssignments -- a GameServer hosting no world is always a configuration mistake.");

host.Services.GetRequiredService<ZoneRegistry>().Initialize(hostedMaps);

await host.RunAsync();
