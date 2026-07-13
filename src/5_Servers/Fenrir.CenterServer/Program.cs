using Fenrir.CenterServer;
using Fenrir.CenterServer.Hosting;
using Fenrir.Cluster;
using Fenrir.Cluster.EventBus;
using Fenrir.Cluster.Party;
using Fenrir.Cluster.WorldState;
using Fenrir.Data;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Security;
using Fenrir.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorldEventBroadcaster = Fenrir.Cluster.WorldState.ICenterLinkBroadcaster;
using PacketBroadcaster = Fenrir.Cluster.ICenterLinkBroadcaster;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFenrirDefaults();

builder.AddFenrirData();

builder.Services.AddFenrirCluster();

builder.Services.Configure<CenterServerOptions>(builder.Configuration.GetSection(CenterServerOptions.SectionName));

builder.Services.AddFenrirCenterLinkAuth(sp =>
    sp.GetRequiredService<IOptions<CenterServerOptions>>().Value.SharedSecret);

// Center authoritative world-state aggregates (single writer for the whole cluster). Registered here rather
// than in AddFenrirCluster because that extension is shared by Game/Login servers -- these authorities are
// Center-only. This lot the machinery is DORMANT (shards still author world state; Lot 5 flips the gate).
builder.Services.AddSingleton<IWorldStateAuthority, WorldStateAuthority>();
builder.Services.AddSingleton<IHeroRankAuthority, HeroRankAuthority>();
builder.Services.AddSingleton<TowerStoreAuthority>();
builder.Services.AddSingleton<CenterAuthorityPreloadService>();

// Registre des liens S2S authentifiés : impl UNIQUE des 4 seams de diffusion sortante (fan-out world-event op33,
// fan-out paquet typé op57, balayage d'inactivité, unicast close-proxy). Le CenterServerHost y (dés)inscrit les
// sessions ; les hosts de cadence et les handlers Center le consomment via les interfaces.
builder.Services.AddSingleton<CenterLinkRegistry>();
builder.Services.AddSingleton<WorldEventBroadcaster>(sp => sp.GetRequiredService<CenterLinkRegistry>());
builder.Services.AddSingleton<PacketBroadcaster>(sp => sp.GetRequiredService<CenterLinkRegistry>());
builder.Services.AddSingleton<ICenterPeerRegistry>(sp => sp.GetRequiredService<CenterLinkRegistry>());
builder.Services.AddSingleton<ICenterCloseProxyRelay>(sp => sp.GetRequiredService<CenterLinkRegistry>());

// Audit write-behind game.EventLog (l'ingesteur op33 y trace les tSort droppés en catégorie AntiCheat).
builder.Services.AddSingleton<CenterEventLogHost>();
builder.Services.AddSingleton<IEventLogQueue>(sp => sp.GetRequiredService<CenterEventLogHost>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CenterEventLogHost>());

// Ingesteur du bus d'events monde op33 (dep du WorldEventCenterHandler auto-enregistré par AddGeneratedPacketHandlers).
builder.Services.AddSingleton<CenterWorldEventIngestor>();

// Party op57 : roster autoritaire volatil + store en mémoire (le handler est auto-enregistré).
builder.Services.AddSingleton<PartyRosterAuthority>();
builder.Services.AddSingleton<IPartyRosterStore, InMemoryPartyRosterStore>();

// Machine de phase Regular War (Zone049) : autorité center-écrit + host de cadence temps-réel.
builder.Services.AddSingleton<RegularWarPhaseAuthority>();
builder.Services.AddHostedService<RegularWarPhaseHost>();

// Seams de persistance world-state (impls DB adossées aux procs game.* ; 2 nouvelles procs = correctifs Bug 3/4).
builder.Services.AddSingleton<ICenterHeroRankStore, CenterHeroRankStore>();
builder.Services.AddSingleton<ICenterTribeScoreSource, CenterTribeScoreSource>();
builder.Services.AddSingleton<ICenterDailyRewardReset, CenterDailyRewardReset>();

// Real-time cadence hosts. Registered BEFORE CenterServerHost so that on shutdown (reverse-registration stop
// order) the accept loop halts and drains its in-flight links FIRST, and WorldStateFlushHost's mandatory
// final flush runs LAST -- no mutation can race the final flush.
builder.Services.AddHostedService<WorldStateFlushHost>();
builder.Services.AddHostedService<HeroRankRolloverHost>();
builder.Services.AddHostedService<DailyResetHost>();
builder.Services.AddHostedService<GuildBuffExpiryHost>();
builder.Services.AddHostedService<PeerLivenessHost>();

builder.Services.AddHostedService<CenterServerHost>();

var host = builder.Build();

PacketHandlerHub.Initialize(host.Services);

// Preload every authoritative aggregate synchronously before RunAsync, under a bounded 60s budget -- matching
// the GameServer boot rule that every cache a tick/connection reads must finish loading first. A load failure
// propagates and refuses boot rather than serving empty authoritative state.
using (var preloadCts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
{
    var preloader = host.Services.GetRequiredService<CenterAuthorityPreloadService>();
    await preloader.PreloadAllAsync(preloadCts.Token);
}

await host.RunAsync();
