using Fenrir.Security.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Security.CenterLink;

public static class CenterLinkAuthServiceCollectionExtensions
{
    public static IServiceCollection AddFenrirCenterLinkAuth(
        this IServiceCollection services,
        Func<IServiceProvider, string?> sharedSecretAccessor)
    {
        ArgumentNullException.ThrowIfNull(sharedSecretAccessor);

        services.AddSingleton<ICenterLinkAuthenticator>(sp => new CenterLinkAuthenticator(sharedSecretAccessor(sp)));
        return services;
    }

    public static IServiceCollection AddFenrirCenterLinkAuth(this IServiceCollection services, string? sharedSecret)
    {
        services.AddSingleton<ICenterLinkAuthenticator>(new CenterLinkAuthenticator(sharedSecret));
        return services;
    }
}
