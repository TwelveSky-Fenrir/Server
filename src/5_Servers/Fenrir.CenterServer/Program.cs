using Fenrir.CenterServer;
using Fenrir.CenterServer.Hosting;
using Fenrir.Cluster;
using Fenrir.Cluster.EventBus;
using Fenrir.Cluster.Party;
using Fenrir.Cluster.WorldState;
using Fenrir.Data;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Security.CenterLink;
using Fenrir.ServiceDefaults;
using Microsoft.Extensions.Options;
using WorldEventBroadcaster = Fenrir.Cluster.WorldState.ICenterLinkBroadcaster;
using PacketBroadcaster = Fenrir.Cluster.Party.ICenterLinkBroadcaster;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFenrirDefaults();

builder.AddFenrirData();

builder.Services.AddFenrirCluster();

builder.Services.Configure<CenterServerOptions>(builder.Configuration.GetSection(CenterServerOptions.SectionName));

builder.Services.AddFenrirCenterLinkAuth(sp =>
    sp.GetRequiredService<IOptions<CenterServerOptions>>().Value.SharedSecret);

builder.Services.AddSingleton<IWorldStateAuthority, WorldStateAuthority>();
builder.Services.AddSingleton<IHeroRankAuthority, HeroRankAuthority>();
builder.Services.AddSingleton<TowerStoreAuthority>();
builder.Services.AddSingleton<CenterAuthorityPreloadService>();

builder.Services.AddSingleton<CenterLinkRegistry>();
builder.Services.AddSingleton<WorldEventBroadcaster>(sp => sp.GetRequiredService<CenterLinkRegistry>());
builder.Services.AddSingleton<PacketBroadcaster>(sp => sp.GetRequiredService<CenterLinkRegistry>());
builder.Services.AddSingleton<ICenterPeerRegistry>(sp => sp.GetRequiredService<CenterLinkRegistry>());
builder.Services.AddSingleton<ICenterCloseProxyRelay>(sp => sp.GetRequiredService<CenterLinkRegistry>());

builder.Services.AddSingleton<CenterEventLogHost>();
builder.Services.AddSingleton<IEventLogQueue>(sp => sp.GetRequiredService<CenterEventLogHost>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CenterEventLogHost>());

builder.Services.AddSingleton<CenterWorldEventIngestor>();

builder.Services.AddSingleton<PartyRosterAuthority>();
builder.Services.AddSingleton<IPartyRosterStore, InMemoryPartyRosterStore>();

builder.Services.AddSingleton<RegularWarPhaseAuthority>();
builder.Services.AddHostedService<RegularWarPhaseHost>();

builder.Services.AddSingleton<ICenterHeroRankStore, CenterHeroRankStore>();
builder.Services.AddSingleton<ICenterTribeScoreSource, CenterTribeScoreSource>();
builder.Services.AddSingleton<ICenterDailyRewardReset, CenterDailyRewardReset>();

builder.Services.AddHostedService<WorldStateFlushHost>();
builder.Services.AddHostedService<HeroRankRolloverHost>();
builder.Services.AddHostedService<DailyResetHost>();
builder.Services.AddHostedService<GuildBuffExpiryHost>();
builder.Services.AddHostedService<PeerLivenessHost>();

builder.Services.AddHostedService<CenterServerHost>();

// Le graphe DI est le seul endroit où un déplacement de code casse en silence : une inscription perdue
// s'injecte en `null` au lieu de lever. Avant Build() pour que le dump existe même si Build() échoue.
DiGraphDump.WriteIfRequested(builder.Services, "centerserver");

var host = builder.Build();

CenterPacketHandlerHub.Initialize(host.Services);

using (var preloadCts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
{
    var preloader = host.Services.GetRequiredService<CenterAuthorityPreloadService>();
    await preloader.PreloadAllAsync(preloadCts.Token);
}

await host.RunAsync();

/// <summary>
///     Vidage ordonné de <see cref="IServiceCollection" /> vers un fichier, un descripteur par ligne, pour
///     comparer au <c>diff</c> le graphe DI de deux commits. Inerte tant que <c>FENRIR_DUMP_DI</c> ne vaut pas 1.
/// </summary>
internal static class DiGraphDump
{
    internal static void WriteIfRequested(IServiceCollection services, string serverName)
    {
        if (Environment.GetEnvironmentVariable("FENRIR_DUMP_DI") != "1")
            return;

        // Deux instances d'un même projet (les shards du GameServer) partagent AppContext.BaseDirectory :
        // au-delà d'un shard, donner son propre FENRIR_DUMP_DI_PATH à chacune.
        var path = Environment.GetEnvironmentVariable("FENRIR_DUMP_DI_PATH");
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(AppContext.BaseDirectory, $"di-graph.{serverName}.txt");

        // LF forcé : deux dumps produits sur des OS différents doivent rester comparables ligne à ligne.
        using var writer = new StreamWriter(path, false) { NewLine = "\n" };

        writer.WriteLine($"# fenrir di-graph | {serverName} | {services.Count} descripteurs | ordre d'enregistrement");
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

    /// <summary>
    ///     Extrait X du type <c>Func&lt;IServiceProvider, X&gt;</c> du délégué, sans l'invoquer ni réfléchir sur
    ///     ses membres. Sans cela, les fabriques d'IHostedService rendraient toutes la même ligne et l'ordre
    ///     d'enregistrement -- qui est l'ordre d'arrêt inversé -- deviendrait illisible.
    /// </summary>
    private static string FactoryTarget(Func<IServiceProvider, object>? factory)
    {
        var name = factory?.GetType().ToString() ?? "<null>";
        var firstArgument = name.IndexOf(',');

        return firstArgument > 0 && name[^1] == ']' ? name[(firstArgument + 1)..^1].Trim() : name;
    }
}
