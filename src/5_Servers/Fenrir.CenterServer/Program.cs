using Fenrir.CenterServer;
using Fenrir.Cluster;
using Fenrir.Data;
using Fenrir.Security;
using Fenrir.ServiceDefaults;
using Microsoft.Extensions.Options;

// Composition du CenterServer (Lot F4 — CenterServer S2S fonctionnel) — coordination cross-zone (fidèle à
// ts25center), fonde le module Fenrir.Cluster. Boot LAZY : l'accept-loop TCP interne (:12003) est armée par
// CenterServerHost, aucun connect() bloquant au démarrage.
var builder = Host.CreateApplicationBuilder(args);

builder.AddFenrirDefaults();

// Repos de l'annuaire runtime (IGameServerDirectoryRepository + IShardMapAssignmentRepository) dont dépend
// l'IZoneDirectory du module Cluster ; adosse le Center à la même vérité durable que Login/GameServer.
builder.AddFenrirData();

// Module Cluster : IZoneDirectory + surface de dispatch S2S entrante (CenterFrameDispatcher + handlers Center).
// Doit venir APRÈS AddFenrirData (dépendance de composition).
builder.Services.AddFenrirCluster();

builder.Services.Configure<CenterServerOptions>(builder.Configuration.GetSection(CenterServerOptions.SectionName));

// Auth du lien S2S : HMAC challenge-response à secret partagé (env Center__SharedSecret → CenterServerOptions).
// Fail-closed si le secret est vide (aucun lien authentifiable). Durcit la faille legacy #8.
builder.Services.AddFenrirCenterLinkAuth(sp =>
    sp.GetRequiredService<IOptions<CenterServerOptions>>().Value.SharedSecret);

builder.Services.AddHostedService<CenterServerHost>();

var host = builder.Build();

// Ponte le MessageDispatcher généré sur le provider DI (résolution des handlers Center) — AVANT que l'accept-loop
// n'accepte des liens, comme LoginServer/GameServer le font pour leurs propres handlers.
PacketHandlerHub.Initialize(host.Services);

await host.RunAsync();
