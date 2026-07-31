using Fenrir.Application.Login;
using Fenrir.Application.Login.Handlers.Extensions;
using Fenrir.Application.Login.Hosting;
using Fenrir.Application.Login.Hosting.Extensions;
using Fenrir.Application.Login.Services.Extensions;
using Fenrir.Cluster.Client.Link;
using Fenrir.Core.Abstractions;
using Fenrir.Data;
using Fenrir.Domain.Login;
using Fenrir.LoginServer;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Security;
using Fenrir.Security.RateLimiting;
using Fenrir.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);
builder.AddFenrirDefaults();
builder.AddFenrirData();
builder.Services.AddFenrirSecurity();

builder.Services.Configure<LoginServerOptions>(builder.Configuration.GetSection("Login"));
builder.Services.AddLoginDomain();
builder.Services.AddLoginServices();
// Enregistre AVANT AddGameHosting/AddLoginHosting : l'ordre d'enregistrement est l'ordre de
// demarrage, donc l'INVERSE de l'ordre d'arret. En dernier, CenterLinkClientHost s'arretait EN
// PREMIER et coupait l'uplink Center pendant que le hote de connexions drainait encore les joueurs
// et que les sept pompes de relais cross-shard tournaient toujours.
builder.Services.AddCenterLinkClient(o =>
{
    o.Endpoint = builder.Configuration["Center:Endpoint"];
    o.SharedSecret = builder.Configuration["Center:SharedSecret"];
});

builder.Services.AddLoginHosting();
builder.Services.AddLoginHandlers();

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<ISessionRateLimiter, SessionRateLimiter>();

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

namespace Fenrir.LoginServer
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
