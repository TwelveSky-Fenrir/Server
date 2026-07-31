using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Extensions;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Handlers.Extensions;
using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Hosting.Extensions;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Fenrir.Application.Game.Services.Extensions;
using Fenrir.Cluster.Client.Link;
using Fenrir.Core.Abstractions;
using Fenrir.Data;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Progression;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Domain.Game.GameData.Extensions;
using Fenrir.GameServer;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Security;
using Fenrir.Security.RateLimiting;
using Fenrir.ServiceDefaults;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.AddFenrirDefaults();
builder.AddFenrirData();
builder.Services.AddFenrirSecurity();

builder.Services.Configure<HostOptions>(o => o.StartupTimeout = TimeSpan.FromSeconds(45));

builder.Services.Configure<GameServerOptions>(builder.Configuration.GetSection("Game"));

builder.Services.AddSingleton<IWorldDataRepository, WorldDataRepository>();
builder.Services.AddSingleton<IMonsterBossRespawnTimerRepository, MonsterBossRespawnTimerRepository>();

builder.Services.AddGameDomain();
builder.Services.AddWorldData();
builder.Services.AddGameServices();
// Enregistre AVANT AddGameHosting/AddLoginHosting : l'ordre d'enregistrement est l'ordre de
// demarrage, donc l'INVERSE de l'ordre d'arret. En dernier, CenterLinkClientHost s'arretait EN
// PREMIER et coupait l'uplink Center pendant que le hote de connexions drainait encore les joueurs
// et que les sept pompes de relais cross-shard tournaient toujours.
builder.Services.AddCenterLinkClient(o =>
{
    o.Endpoint = builder.Configuration["Center:Endpoint"];
    o.SharedSecret = builder.Configuration["Center:SharedSecret"];
});

builder.Services.AddGameHosting();
builder.Services.AddGameHandlers();

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<ISessionRateLimiter, SessionRateLimiter>();

DiGraphDump.WriteIfRequested(builder.Services, "gameserver");

var host = builder.Build();

ZonePacketHandlerHub.Initialize(host.Services);

var bootLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Fenrir.GameServer.Boot");

var bootStep = "(unknown)";
byte shardId;
IReadOnlyList<short> hostedMaps;

using var bootCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

try
{
    bootStep = "WorldDataLoader.InitializeAsync";
    await host.Services.GetRequiredService<WorldDataLoader>().InitializeAsync(bootCts.Token);

    bootStep = "TribeConversionCatalogLoader.InitializeAsync";
    await host.Services.GetRequiredService<TribeConversionCatalogLoader>()
        .InitializeAsync(host.Services.GetRequiredService<IWorldDataRepository>(), bootCts.Token);

    bootStep = "CommerceCatalogCache.RefreshAllAsync";
    await host.Services.GetRequiredService<CommerceCatalogCache>()
        .RefreshAllAsync(host.Services.GetRequiredService<IWorldDataRepository>(), bootCts.Token);

    bootStep = "WorldStateService.InitializeAsync";
    await host.Services.GetRequiredService<WorldStateService>().InitializeAsync(bootCts.Token);

    bootStep = "GuildRankingCache.RefreshAsync";
    await host.Services.GetRequiredService<GuildRankingCache>()
        .RefreshAsync(host.Services.GetRequiredService<IGuildRepository>(), bootCts.Token);

    bootStep = "TowerWarState.InitializeAsync";
    await host.Services.GetRequiredService<TowerWarState>()
        .InitializeAsync(host.Services.GetRequiredService<ITowerRepository>(), bootCts.Token);

    bootStep = "MonsterBossRespawnTracker.InitializeAsync";
    await host.Services.GetRequiredService<MonsterBossRespawnTracker>()
        .InitializeAsync(host.Services.GetRequiredService<IMonsterBossRespawnTimerRepository>(),
            bootCts.Token);

    bootStep = "IShardMapAssignmentRepository.GetHostedMapsAsync";
    shardId = host.Services.GetRequiredService<IOptions<GameServerOptions>>().Value.ShardId;
    hostedMaps = await host.Services.GetRequiredService<IShardMapAssignmentRepository>()
        .GetHostedMapsAsync(shardId, bootCts.Token);

    if (hostedMaps.Count == 0)
        throw new InvalidOperationException(
            $"No maps assigned to shard {shardId} in admin.ShardMapAssignments -- a GameServer hosting no world is always a configuration mistake.");

    bootStep = "ZonePortRangeGuard.EnsureAllPortsWithinReservedBlock";
    var portOptions = host.Services.GetRequiredService<IOptions<GameServerOptions>>().Value;
    ZonePortRangeGuard.EnsureAllPortsWithinReservedBlock(
        shardId,
        hostedMaps,
        portOptions.ZoneBasePort,
        portOptions.ZonePortRangeStart,
        portOptions.ZonePortRangeEnd,
        portOptions.ReservedPorts
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => int.TryParse(value, out var port) ? port : -1)
            .Where(static port => port > 0)
            .ToArray());

    bootStep = "ShardPartitionGuard.EnsureNoOverlapAsync";
    await ShardPartitionGuard.EnsureNoOverlapAsync(shardId, hostedMaps,
        host.Services.GetRequiredService<IGameServerDirectoryRepository>(),
        host.Services.GetRequiredService<IShardMapAssignmentRepository>(),
        bootCts.Token);
}
catch (OperationCanceledException ex) when (bootCts.IsCancellationRequested)
{
    bootLogger.LogCritical(ex,
        "Fenrir.GameServer boot HUNG during step '{BootStep}' -- exceeded the 60s boot-step timeout with no " +
        "exception of its own, so the process is exiting instead of waiting forever. This step's own await " +
        "never completed or threw; investigate that specific dependency directly (a debugger/dotnet-dump " +
        "attach while it is stuck names the exact suspended call).", bootStep);
    throw;
}
catch (Exception ex)
{
    bootLogger.LogCritical(ex,
        "Fenrir.GameServer boot failed during step '{BootStep}' -- process will exit without accepting connections",
        bootStep);
    throw;
}

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

bootLogger.LogInformation(
    "GameServer preload complete; entering host.RunAsync() now -- zone listeners, the directory heartbeat and the " +
    "Center uplink start here. Expect 'Application started', then 'GameServerDirectory heartbeat host started', then " +
    "'registered in runtime.GameServerDirectory'.");

try
{
    await host.RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    bootLogger.LogCritical(ex,
        "GameServer host.RunAsync() failed to complete startup -- a hosted-service StartAsync (if this is a 45s " +
        "StartupTimeout) or a hosted-service/singleton construction faulted during IHostedService materialization.");
    throw;
}

namespace Fenrir.GameServer
{
    internal static class DiGraphDump
    {
        internal static void WriteIfRequested(IServiceCollection services, string serverName)
        {
            if (Environment.GetEnvironmentVariable("FENRIR_DUMP_DI") != "1")
                return;

            var path = Environment.GetEnvironmentVariable("FENRIR_DUMP_DI_PATH");
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(AppContext.BaseDirectory, $"di-graph.{serverName}.txt");

            using var writer = new StreamWriter(path, false) { NewLine = "\n" };

            writer.WriteLine(
                $"# fenrir di-graph | {serverName} | {services.Count} descripteurs | ordre d'enregistrement");
            writer.WriteLine("# index | ServiceType | implementation | Lifetime");
            writer.WriteLine("# une insertion renumérote tout ce qui suit ; pour isoler le delta réel :");
            writer.WriteLine("#   diff <(cut -d'|' -f2- avant.txt) <(cut -d'|' -f2- apres.txt)");

            for (var i = 0; i < services.Count; i++)
            {
                var descriptor = services[i];
                writer.WriteLine($"{i:D4} | {descriptor.ServiceType} | {Describe(descriptor)} | {descriptor.Lifetime}");
            }
        }

        private static string Describe(ServiceDescriptor descriptor)
        {
            if (descriptor.IsKeyedService)
            {
                var key = $"keyed[{descriptor.ServiceKey}] ";
                if (descriptor.KeyedImplementationType is { } keyedType) return $"{key}type {keyedType}";
                if (descriptor.KeyedImplementationInstance is { } keyedInstance)
                    return $"{key}instance {keyedInstance.GetType()}";

                return $"{key}factory";
            }

            if (descriptor.ImplementationType is { } type) return $"type {type}";
            if (descriptor.ImplementationInstance is { } instance) return $"instance {instance.GetType()}";

            return $"factory {FactoryTarget(descriptor.ImplementationFactory)}";
        }

        private static string FactoryTarget(Func<IServiceProvider, object>? factory)
        {
            var name = factory?.GetType().ToString() ?? "<null>";
            var firstArgument = name.IndexOf(',');

            return firstArgument > 0 && name[^1] == ']' ? name[(firstArgument + 1)..^1].Trim() : name;
        }
    }
}
