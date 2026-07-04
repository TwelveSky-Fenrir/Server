using Fenrir.Application.Login;
using Fenrir.Application.Login.Dispatching;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Dispatch;
using Fenrir.Data;
using Fenrir.LoginServer;
using Fenrir.Network.RateLimiting;
using Fenrir.Network.Sessions;
using Fenrir.ServiceDefaults;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddFenrirData();

builder.Services.Configure<LoginServerOptions>(builder.Configuration.GetSection("Login"));
builder.Services.AddSingleton<IValidateOptions<LoginServerOptions>, LoginServerOptionsValidator>();
builder.Services.AddOptions<LoginServerOptions>().ValidateOnStart();
builder.Services.AddLoginHandlers();

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<ISessionRateLimiter, SessionRateLimiter>();
builder.Services.AddSingleton<IFrameDispatcher, LoginFrameDispatcher>();
builder.Services.AddHostedService<LoginConnectionHost>();

var host = builder.Build();

// Must run before LoginConnectionHost starts accepting connections: MessageDispatcher resolves handlers through this provider.
PacketHandlerHub.Initialize(host.Services);

host.Run();
