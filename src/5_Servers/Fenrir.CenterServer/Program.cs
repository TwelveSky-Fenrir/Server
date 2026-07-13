using Fenrir.CenterServer;
using Fenrir.Cluster;
using Fenrir.Data;
using Fenrir.Security;
using Fenrir.ServiceDefaults;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFenrirDefaults();

builder.AddFenrirData();

builder.Services.AddFenrirCluster();

builder.Services.Configure<CenterServerOptions>(builder.Configuration.GetSection(CenterServerOptions.SectionName));

builder.Services.AddFenrirCenterLinkAuth(sp =>
    sp.GetRequiredService<IOptions<CenterServerOptions>>().Value.SharedSecret);

builder.Services.AddHostedService<CenterServerHost>();

var host = builder.Build();

PacketHandlerHub.Initialize(host.Services);

await host.RunAsync();
