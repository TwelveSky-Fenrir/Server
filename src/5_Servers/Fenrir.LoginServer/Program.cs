using Fenrir.Application.Login;
using Fenrir.Application.Login.Handlers.Extensions;
using Fenrir.Application.Login.Hosting;
using Fenrir.Application.Login.Hosting.Extensions;
using Fenrir.Application.Login.Services.Extensions;
using Fenrir.Cluster.Link;
using Fenrir.Data;
using Fenrir.Domain.Login;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Security;
using Fenrir.Security.Abstractions;
using Fenrir.Security.RateLimiting;
using Fenrir.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);
builder.AddFenrirDefaults();
builder.AddFenrirData();
builder.Services.AddFenrirSecurity();

builder.Services.Configure<LoginServerOptions>(builder.Configuration.GetSection("Login"));
builder.Services.AddLoginDomain();
builder.Services.AddLoginServices();
builder.Services.AddLoginHosting();
builder.Services.AddLoginHandlers();

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<ISessionRateLimiter, SessionRateLimiter>();

builder.Services.AddCenterLinkClient(o =>
{
    o.Endpoint = builder.Configuration["Center:Endpoint"];
    o.SharedSecret = builder.Configuration["Center:SharedSecret"];
});

// Le graphe DI est le seul endroit où un déplacement de code casse en silence : une inscription perdue
// s'injecte en `null` au lieu de lever. Avant Build() pour que le dump existe même si Build() échoue.
DiGraphDump.WriteIfRequested(builder.Services, "loginserver");

var host = builder.Build();

LoginPacketHandlerHub.Initialize(host.Services);

var bootLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Fenrir.LoginServer.Boot");

var bootStep = "ServerQuotaRefreshHost.InitializeAsync";
try
{
    await host.Services.GetRequiredService<ServerQuotaRefreshHost>().InitializeAsync(CancellationToken.None);
}
catch (Exception ex)
{
    bootLogger.LogCritical(ex,
        "Fenrir.LoginServer boot failed during step '{BootStep}' -- process will exit without accepting connections",
        bootStep);
    throw;
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
