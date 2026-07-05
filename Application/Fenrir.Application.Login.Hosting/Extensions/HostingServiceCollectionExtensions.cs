using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Login.Hosting.Extensions;

/// <summary>Registers the LoginServer's TCP connection listener.</summary>
public static class HostingServiceCollectionExtensions
{
    public static IServiceCollection AddLoginHosting(this IServiceCollection services)
    {
        services.AddHostedService<LoginConnectionHost>();

        return services;
    }
}
