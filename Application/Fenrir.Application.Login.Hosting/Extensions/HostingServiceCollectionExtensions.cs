using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
