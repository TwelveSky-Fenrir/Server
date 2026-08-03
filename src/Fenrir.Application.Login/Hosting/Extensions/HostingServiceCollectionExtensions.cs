using Fenrir.Application.Login.Sessions;
using Fenrir.Domain.Login;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Login;
using Fenrir.Security.FloodProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting.Extensions;

public static class HostingServiceCollectionExtensions
{
    public static IServiceCollection AddLoginHosting(this IServiceCollection services)
    {
        services.AddSingleton<IOpcodeFrameSizeProvider>(LoginOpcodeRegistry.Provider);
        services.AddSingleton<LoginIdleClock>();

        services.AddSingleton<IValidateOptions<LoginSocketAdmissionOptions>, LoginSocketAdmissionOptionsValidator>();
        services.AddOptions<LoginSocketAdmissionOptions>().ValidateOnStart();
        services.AddSingleton(sp => new LoginSocketAdmissionGate(
            sp.GetRequiredService<IOptions<LoginSocketAdmissionOptions>>().Value.MaxConcurrentConnections));

        services.AddHostedService<LoginConnectionHost>();

        services.AddSingleton<LoginSessionLivenessSweep>();
        services.AddHostedService<LoginSessionLivenessSweepHost>();

        services.AddHostedService<AccountSessionLivenessHost>();
        services.AddHostedService<AccountSessionReapHost>();

        services.AddHostedService<SessionTicketPurgeHost>();

        services.AddSingleton<ServerQuotaRefreshHost>();
        services.AddHostedService(sp => sp.GetRequiredService<ServerQuotaRefreshHost>());

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<LoginServerOptions>>().Value;
            var firewallRules = sp.GetRequiredService<IFirewallRuleRepository>();
            var registry = sp.GetRequiredService<SessionRegistry>();
            var gmAllowlist = sp.GetRequiredService<IGmAllowlistRepository>();

            return new IpFloodGuard(
                opts.MaxConnectionsPerIp,
                opts.MaxProtocolViolationsPerIpPerHour,
                firewallRules.BlockAsync,
                registry,
                logger: sp.GetRequiredService<ILogger<IpFloodGuard>>(),
                isOperatorAllowlistedAsync: gmAllowlist.IsAllowedAsync);
        });

        services.AddSingleton(sp => new FirewallAllowlistReconcileService(
            sp.GetRequiredService<IFirewallRuleRepository>().ReconcileAllowlistAsync,
            logger: sp.GetService<ILogger<FirewallAllowlistReconcileService>>()));
        services.AddHostedService<FirewallAllowlistReconcileHost>();

        return services;
    }
}
