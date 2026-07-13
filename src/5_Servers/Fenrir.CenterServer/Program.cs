using Fenrir.CenterServer;
using Fenrir.ServiceDefaults;

// Composition du CenterServer (Lot F2) — coordination cross-zone (fidèle à ts25center), fonde le module
// Fenrir.Cluster (rempli au Lot F4). Boot en hosted-services ordonnés (correctif F8).
var builder = Host.CreateApplicationBuilder(args);

builder.AddFenrirDefaults();

builder.Services.Configure<CenterServerOptions>(builder.Configuration.GetSection(CenterServerOptions.SectionName));
builder.Services.AddHostedService<CenterServerHost>();

// TODO(F4) : AddFenrirData(), module Cluster (GameServerDirectory, relais consolidés, world-state autoritaire),
//            listener TCP interne + comms inter-serveurs TCP.
var host = builder.Build();
await host.RunAsync();
