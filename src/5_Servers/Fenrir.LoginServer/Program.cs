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
