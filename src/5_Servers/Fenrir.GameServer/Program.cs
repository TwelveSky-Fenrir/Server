using Fenrir.Cluster.Link;
using Fenrir.Security;
using Fenrir.Security.RateLimiting;
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
using Fenrir.Application.Game;
using Fenrir.Security.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.AddFenrirDefaults();
builder.AddFenrirData();
builder.Services.AddFenrirSecurity();

// Filet anti-hang au démarrage : host.RunAsync() n'a AUCUN timeout (contrairement au préchargement borné par
// bootCts plus bas). Si un IHostedService.StartAsync -- ou un ctor de singleton résolu lors de la matérialisation
// de IEnumerable<IHostedService> par le host -- bloque host.StartAsync, on préfère une exception explicite après
// 45 s à un hang silencieux et infini qui laisse le shard absent de runtime.GameServerDirectory.
builder.Services.Configure<HostOptions>(o => o.StartupTimeout = TimeSpan.FromSeconds(45));

builder.Services.Configure<GameServerOptions>(builder.Configuration.GetSection("Game"));
builder.Services.AddGameDomain();
builder.Services.AddWorldData();
builder.Services.AddGameServices();
builder.Services.AddGameHosting();
builder.Services.AddGameHandlers();

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<ISessionRateLimiter, SessionRateLimiter>();

// Lien S2S sortant vers le CenterServer (le shard devient un pair authentifié : substrat du push d'events
// monde op33 et de la réception du fan-out — la bascule effective shard->center est le Lot 5). Boot LAZY :
// reconnexion backoff, l'absence du Center ne bloque jamais l'acceptation des clients de zone.
builder.Services.AddCenterLinkClient(o =>
{
    o.Endpoint = builder.Configuration["Center:Endpoint"];
    o.SharedSecret = builder.Configuration["Center:SharedSecret"];
});

// Capturé AVANT Build : le host matérialise tous les IHostedService d'un bloc (un IEnumerable), masquant lequel
// bloque. On garde la liste ordonnée des descripteurs pour les sonder UN PAR UN plus bas.
var hostedServiceDescriptors = builder.Services
    .Where(d => d.ServiceType == typeof(IHostedService))
    .ToArray();

var host = builder.Build();

PacketHandlerHub.Initialize(host.Services);

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

// SONDE DE CONSTRUCTION : le host fige pendant la matérialisation de IEnumerable<IHostedService> (exécution des
// constructeurs) -- étape NON bornée par HostOptions.StartupTimeout et NON validée en Production (ValidateOnBuild
// off). On construit ici chaque hosted-service INDIVIDUELLEMENT : la dernière ligne « probing [i] X » SANS
// « constructed OK » qui suit nomme le service (ou une de ses dépendances) dont le constructeur bloque.
bootLogger.LogInformation(
    "Probing {Count} hosted-service constructions one by one to pinpoint any blocking constructor (the last " +
    "'probing [i] ...' with no following 'constructed OK' is the culprit)...", hostedServiceDescriptors.Length);
for (var i = 0; i < hostedServiceDescriptors.Length; i++)
{
    var descriptor = hostedServiceDescriptors[i];
    var label = descriptor.ImplementationType?.FullName
                ?? descriptor.ImplementationInstance?.GetType().FullName
                ?? descriptor.ImplementationFactory?.Method.DeclaringType?.FullName
                ?? "(factory)";
    bootLogger.LogInformation("  probing [{Index}]: {Label}", i, label);
    try
    {
        var instance = descriptor.ImplementationInstance
                       ?? (descriptor.ImplementationFactory is not null
                           ? descriptor.ImplementationFactory(host.Services)
                           : ActivatorUtilities.CreateInstance(host.Services, descriptor.ImplementationType!));
        bootLogger.LogInformation("    constructed OK [{Index}]: {Type}", i, instance.GetType().Name);
    }
    catch (Exception probeEx)
    {
        bootLogger.LogWarning(probeEx,
            "    construction of [{Index}] {Label} threw (a throw is not the hang -- keep going)", i, label);
    }
}

bootLogger.LogInformation(
    "All {Count} hosted-service constructions probed OK -- the startup hang is NOT a blocking constructor; it is in " +
    "StartAsync or the host pipeline (re-examine with that in mind).", hostedServiceDescriptors.Length);

bootLogger.LogInformation(
    "GameServer preload complete; entering host.RunAsync() now -- zone listeners, the directory heartbeat and the " +
    "Center uplink start HERE. Expect next: 'Application started', then 'GameServerDirectory heartbeat host started' " +
    "then 'registered in runtime.GameServerDirectory'. If NONE of these appear, host startup is hanging and the 45s " +
    "StartupTimeout will surface it as a CRITICAL with the blocking stack.");

try
{
    await host.RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    bootLogger.LogCritical(ex,
        "GameServer host.RunAsync() failed to complete startup -- a hosted-service StartAsync or a singleton ctor " +
        "resolved during IHostedService materialization blocked host start (no 'Application started' reached), so the " +
        "directory heartbeat never ran and the shard stayed absent from runtime.GameServerDirectory. The exception " +
        "above names the exact blocking call.");
    throw;
}
