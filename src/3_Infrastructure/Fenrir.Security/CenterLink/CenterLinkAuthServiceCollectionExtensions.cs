using Fenrir.Security.Abstractions;
using Fenrir.Security.CenterLink;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Security;

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
