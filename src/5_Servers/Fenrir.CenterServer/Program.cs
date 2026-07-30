using Fenrir.CenterServer;
using Fenrir.CenterServer.Hosting;
using Fenrir.Cluster;
using Fenrir.Cluster.EventBus;
using Fenrir.Cluster.Party;
using Fenrir.Cluster.WorldState;
using Fenrir.Data;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Security.CenterLink;
using Fenrir.ServiceDefaults;
using Microsoft.Extensions.Options;
using WorldEventBroadcaster = Fenrir.Cluster.WorldState.ICenterLinkBroadcaster;
using PacketBroadcaster = Fenrir.Cluster.Party.ICenterLinkBroadcaster;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFenrirDefaults();

builder.AddFenrirData();

builder.Services.AddFenrirCluster();

builder.Services.Configure<CenterServerOptions>(builder.Configuration.GetSection(CenterServerOptions.SectionName));

builder.Services.AddFenrirCenterLinkAuth(sp =>
    sp.GetRequiredService<IOptions<CenterServerOptions>>().Value.SharedSecret);

builder.Services.AddSingleton<IWorldStateAuthority, WorldStateAuthority>();
builder.Services.AddSingleton<IHeroRankAuthority, HeroRankAuthority>();
builder.Services.AddSingleton<TowerStoreAuthority>();
builder.Services.AddSingleton<CenterAuthorityPreloadService>();

builder.Services.AddSingleton<CenterLinkRegistry>();
builder.Services.AddSingleton<WorldEventBroadcaster>(sp => sp.GetRequiredService<CenterLinkRegistry>());
builder.Services.AddSingleton<PacketBroadcaster>(sp => sp.GetRequiredService<CenterLinkRegistry>());
builder.Services.AddSingleton<ICenterPeerRegistry>(sp => sp.GetRequiredService<CenterLinkRegistry>());
builder.Services.AddSingleton<ICenterCloseProxyRelay>(sp => sp.GetRequiredService<CenterLinkRegistry>());

builder.Services.AddSingleton<CenterEventLogHost>();
builder.Services.AddSingleton<IEventLogQueue>(sp => sp.GetRequiredService<CenterEventLogHost>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CenterEventLogHost>());

builder.Services.AddSingleton<CenterWorldEventIngestor>();

builder.Services.AddSingleton<PartyRosterAuthority>();
builder.Services.AddSingleton<IPartyRosterStore, InMemoryPartyRosterStore>();

builder.Services.AddSingleton<RegularWarPhaseAuthority>();
builder.Services.AddHostedService<RegularWarPhaseHost>();

builder.Services.AddSingleton<ICenterHeroRankStore, CenterHeroRankStore>();
builder.Services.AddSingleton<ICenterTribeScoreSource, CenterTribeScoreSource>();
builder.Services.AddSingleton<ICenterDailyRewardReset, CenterDailyRewardReset>();

builder.Services.AddHostedService<WorldStateFlushHost>();
builder.Services.AddHostedService<HeroRankRolloverHost>();
builder.Services.AddHostedService<DailyResetHost>();
builder.Services.AddHostedService<GuildBuffExpiryHost>();
builder.Services.AddHostedService<PeerLivenessHost>();

builder.Services.AddHostedService<CenterServerHost>();

var host = builder.Build();

CenterPacketHandlerHub.Initialize(host.Services);

using (var preloadCts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
{
    var preloader = host.Services.GetRequiredService<CenterAuthorityPreloadService>();
    await preloader.PreloadAllAsync(preloadCts.Token);
}

await host.RunAsync();
