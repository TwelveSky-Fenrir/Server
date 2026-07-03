using Fenrir.Application.Game;
using Fenrir.Application.Game.Dispatching;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Dispatch;
using Fenrir.Data;
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
builder.Services.AddSingleton<ZoneRegistry>();
builder.Services.AddHostedService<ZoneTickHost>();

// Same "one instance, three registrations" pattern for a hosted service other code also needs to call directly.
builder.Services.AddSingleton<PositionWriteBehindHost>();
builder.Services.AddSingleton<IWriteBehindFlusher>(sp => sp.GetRequiredService<PositionWriteBehindHost>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<PositionWriteBehindHost>());

builder.Services.AddHostedService<GameServerDirectoryHeartbeat>();
builder.Services.AddHostedService<ZoneConnectionHost>();

var host = builder.Build();

// Must run before ZoneConnectionHost starts accepting connections -- see Fenrir.LoginServer/Program.cs's
// identical PacketHandlerHub.Initialize call for the same reason.
PacketHandlerHub.Initialize(host.Services);

// Hosted services (including ZoneConnectionHost) only start inside host.Run(), so awaiting this here
// guarantees the world.* reference-data cache is fully populated before the first connection is accepted --
// a SQL failure or an unseeded database aborts startup instead of silently serving an empty world.
await host.Services.GetRequiredService<WorldDataLoader>().InitializeAsync(CancellationToken.None);

await host.RunAsync();
