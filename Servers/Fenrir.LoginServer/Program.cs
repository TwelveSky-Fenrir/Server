
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Extensions;
using Fenrir.Application.Login.Handlers.Extensions;
using Fenrir.Application.Login.Hosting;
using Fenrir.Application.Login.Hosting.Extensions;
using Fenrir.Application.Login.Services.Extensions;
using Fenrir.Data;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddFenrirData();

builder.Services.Configure<LoginServerOptions>(builder.Configuration.GetSection("Login"));
builder.Services.AddLoginDomain();
builder.Services.AddLoginServices();
builder.Services.AddLoginHosting();
builder.Services.AddLoginHandlers();

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<ISessionRateLimiter, SessionRateLimiter>();

var host = builder.Build();

PacketHandlerHub.Initialize(host.Services);

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
