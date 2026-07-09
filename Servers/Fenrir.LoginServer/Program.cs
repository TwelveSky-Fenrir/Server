// SECURITY GUARDRAIL -- read before adding any HTTP/gRPC surface to this executable.
// Legacy ts25center bound an unauthenticated cpp-httplib dashboard on 127.0.0.1:2499 with zero
// authentication anywhere in its routing (no set_pre_routing_handler check) and a side-effecting
// GET /Shutdown that armed a cluster-wide kill switch (Server/ts25center/S02_MyServer.cpp:56-82,215-253;
// ServerDocs/10_ts25center/03_HTTP_Dashboard_NonAuth_CRITIQUE.md; ServerDocs/04_SECURITE_ET_DETTE_TECHNIQUE.md
// finding #2). This is the single worst legacy anti-pattern this project's security audits have found.
// Fenrir.LoginServer is deliberately a Microsoft.NET.Sdk.Worker project with no ASP.NET Core reference at
// all (Host.CreateApplicationBuilder, never WebApplication -- see Orchestration/Fenrir.ServiceDefaults'
// own "TCP-socket-only servers" comments), and Tests/Fenrir.IntegrationTests/NoUnauthenticatedHttpSurfaceTests.cs
// regression-tests that this stays true. The day anyone proposes an HTTP/gRPC admin, metrics, or GM surface
// on this process: it must get its own explicit authentication and authorization from line one, must never
// be assumed to inherit ClientSession/game-session auth, must never expose a state-mutating action behind
// an unauthenticated GET (or any verb without a real credential check), and must bind to a scope no
// broader than the ts25 loopback-only precedent unless there is an explicit, reviewed reason to widen it.
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

// Must run before LoginConnectionHost starts accepting connections: MessageDispatcher resolves handlers through this provider.
PacketHandlerHub.Initialize(host.Services);

// Must run before LoginConnectionHost starts accepting connections: the maintenance-lockdown/server-full quota
// gates (LoginService.LoginAsync) read LoginCapacityState synchronously on every login attempt, so it must
// already hold a real admin.ServerQuota.MaxPlayers value by the time the first connection can send one. A
// failed read here is fatal startup (matches legacy MyGame::Init's own fatal-on-failure treatment), unlike
// ServerQuotaRefreshHost's later recurring re-reads.
await host.Services.GetRequiredService<ServerQuotaRefreshHost>().InitializeAsync(CancellationToken.None);

await host.RunAsync();
