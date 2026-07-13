using Fenrir.Network.Abstractions;
using Fenrir.Security.Abstractions;
using Fenrir.Security.CenterLink;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Cluster.Link;

public static class CenterLinkClientServiceCollectionExtensions
{
    /// <summary>
    ///     Wires the outbound CenterServer link into a LoginServer/GameServer: options, the shared-secret
    ///     authenticator, and one <c>CenterLinkClientHost</c> instance surfaced as the concrete host, the
    ///     <see cref="ICenterLink" /> send API, and an <c>IHostedService</c>. The link is lazy — the host boots and
    ///     serves clients regardless of whether the Center is reachable — so there is no <c>WaitFor</c> to add.
    ///     <para>
    ///         If the consumer registers an <see cref="IFrameDispatcher" /> (with its own inbound handlers) it is
    ///         picked up automatically and routes the Center fan-out; otherwise received frames are logged and
    ///         dropped (no client-side inbound handlers exist this lot).
    ///     </para>
    /// </summary>
    public static IServiceCollection AddCenterLinkClient(
        this IServiceCollection services,
        Action<CenterLinkClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        // The uplink authenticates against the CenterServer with the SAME shared secret the server verifies.
        // TryAdd so a host that already registered an authenticator (e.g. via AddFenrirCenterLinkAuth) keeps it.
        services.TryAddSingleton<ICenterLinkAuthenticator>(sp =>
            new CenterLinkAuthenticator(sp.GetRequiredService<IOptions<CenterLinkClientOptions>>().Value.SharedSecret));

        // One instance, three roles. The factory pulls IFrameDispatcher via GetService (not GetRequiredService) so
        // the inbound dispatcher stays OPTIONAL.
        services.AddSingleton<CenterLinkClientHost>(sp => new CenterLinkClientHost(
            sp.GetRequiredService<ILogger<CenterLinkClientHost>>(),
            sp.GetRequiredService<IOptions<CenterLinkClientOptions>>(),
            sp.GetRequiredService<ICenterLinkAuthenticator>(),
            sp.GetService<IFrameDispatcher>()));
        services.AddSingleton<ICenterLink>(sp => sp.GetRequiredService<CenterLinkClientHost>());
        services.AddHostedService(sp => sp.GetRequiredService<CenterLinkClientHost>());

        return services;
    }
}
