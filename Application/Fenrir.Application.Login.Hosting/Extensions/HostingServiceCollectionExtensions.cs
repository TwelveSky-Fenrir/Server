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

        // Forcibly disconnects any Login TCP connection -- pre-auth or post-auth alike -- idle 60+ seconds
        // (Server/ts25login/S07_MyGame01.cpp:37-73), the Login-side counterpart to GameServer's own
        // SessionLivenessSweep. Singleton (not just AddHostedService<T>) so a future direct-resolve caller (e.g.
        // a health check) can share the same instance the generic host also drives.
        services.AddSingleton<LoginSessionLivenessSweep>();
        services.AddHostedService<LoginSessionLivenessSweepHost>();

        // Cross-process duplicate-login kick/refusal: keeps this process's live sessions' runtime.AccountSessions
        // rows warm, and reaps rows any process (Login or Game) abandoned without running its own teardown path.
        services.AddHostedService<AccountSessionLivenessHost>();
        services.AddHostedService<AccountSessionReapHost>();

        // Sweeps runtime.SessionTickets rows left behind by a zone-transfer handshake that never completed
        // (client crash, dropped Login<->Game connection); the table is memory-optimized/SCHEMA_ONLY with no
        // cleanup of its own besides this timer and the create/consume paths' own supersede/always-delete behavior.
        services.AddHostedService<SessionTicketPurgeHost>();

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
