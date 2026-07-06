using Fenrir.Application.Login.Domain;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting.Extensions;

/// <summary>Registers the LoginServer's TCP connection listener.</summary>
public static class HostingServiceCollectionExtensions
{
    public static IServiceCollection AddLoginHosting(this IServiceCollection services)
    {
        services.AddHostedService<LoginConnectionHost>();

        // Cross-process duplicate-login kick/refusal: keeps this process's live sessions' runtime.AccountSessions
        // rows warm, and reaps rows any process (Login or Game) abandoned without running its own teardown path.
        services.AddHostedService<AccountSessionLivenessHost>();
        services.AddHostedService<AccountSessionReapHost>();

        // Keeps LoginCapacityState fresh for the CL_LOGIN_SEND maintenance-lockdown/server-full quota gates.
        // Registered as its own concrete singleton (not just AddHostedService<T>) so Program.cs can resolve it
        // directly and call InitializeAsync() once, synchronously, before the host starts accepting connections
        // -- the same instance the generic host later starts as an IHostedService.
        services.AddSingleton<ServerQuotaRefreshHost>();
        services.AddHostedService(sp => sp.GetRequiredService<ServerQuotaRefreshHost>());

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<LoginServerOptions>>().Value;
            var firewallRules = sp.GetRequiredService<IFirewallRuleRepository>();
            var registry = sp.GetRequiredService<SessionRegistry>();

            return new IpFloodGuard(
                opts.MaxConnectionsPerIp,
                opts.MaxProtocolViolationsPerIpPerHour,
                firewallRules.BlockAsync,
                registry);
        });

        return services;
    }
}
