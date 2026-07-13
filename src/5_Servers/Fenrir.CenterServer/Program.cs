using Fenrir.CenterServer;
using Fenrir.Cluster;
using Fenrir.Data;
using Fenrir.ServiceDefaults;

// Composition du CenterServer (Lot F4 — substrat S2S minimal-viable) — coordination cross-zone (fidèle à
// ts25center), fonde le module Fenrir.Cluster. Boot LAZY : l'accept-loop TCP interne (:12003) est armée par
// CenterServerHost, aucun connect() bloquant au démarrage.
var builder = Host.CreateApplicationBuilder(args);

builder.AddFenrirDefaults();

// Repos de l'annuaire runtime (IGameServerDirectoryRepository + IShardMapAssignmentRepository) dont dépend
// l'IZoneDirectory du module Cluster ; adosse le Center à la même vérité durable que Login/GameServer.
builder.AddFenrirData();

// Module Cluster : enregistre IZoneDirectory (annuaire de zones adossé aux repos runtime d'AddFenrirData).
// Doit venir APRÈS AddFenrirData (dépendance de composition).
builder.Services.AddFenrirCluster();

builder.Services.Configure<CenterServerOptions>(builder.Configuration.GetSection(CenterServerOptions.SectionName));
builder.Services.AddHostedService<CenterServerHost>();

// TODO(F4) : AddFenrirSecurity() + handshake authentifié du lien S2S ; relais consolidés + world-state autoritaire.
var host = builder.Build();
await host.RunAsync();
