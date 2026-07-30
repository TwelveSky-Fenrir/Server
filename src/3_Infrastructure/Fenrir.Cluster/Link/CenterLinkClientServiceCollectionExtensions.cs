using Fenrir.Security.Abstractions;
using Fenrir.Security.CenterLink;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Cluster.Link;

public static class CenterLinkClientServiceCollectionExtensions
{
    public static IServiceCollection AddCenterLinkClient(
        this IServiceCollection services,
        Action<CenterLinkClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        services.TryAddSingleton<ICenterLinkAuthenticator>(sp =>
            new CenterLinkAuthenticator(sp.GetRequiredService<IOptions<CenterLinkClientOptions>>().Value.SharedSecret));

        services.AddSingleton<CenterLinkClientHost>(sp => new CenterLinkClientHost(
            sp.GetRequiredService<ILogger<CenterLinkClientHost>>(),
            sp.GetRequiredService<IOptions<CenterLinkClientOptions>>(),
            sp.GetRequiredService<ICenterLinkAuthenticator>(),
            sp.GetService<ICenterFanOutSink>()));
        services.AddSingleton<ICenterLink>(sp => sp.GetRequiredService<CenterLinkClientHost>());
        services.AddSingleton(sp => new Lazy<ICenterLink>(sp.GetRequiredService<ICenterLink>));
        services.AddHostedService(sp => sp.GetRequiredService<CenterLinkClientHost>());

        return services;
    }
}
