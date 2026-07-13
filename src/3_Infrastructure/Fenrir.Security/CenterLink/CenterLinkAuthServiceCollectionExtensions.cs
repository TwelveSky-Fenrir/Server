using Fenrir.Security.Abstractions;
using Fenrir.Security.CenterLink;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Security;

/// <summary>
/// Enregistrement DI de l'authentificateur du lien serveur-à-serveur (CenterLink). Consommé des <b>deux</b> côtés :
/// le CenterServer (émission du défi + vérification à l'accept) et les clients Login/Game (calcul de la réponse).
/// Le secret est lu de la même source aux deux extrémités : l'env <c>Center__SharedSecret</c> injecté par l'AppHost
/// (côté Center il transite déjà par <c>CenterServerOptions.SharedSecret</c>). L'authentificateur est <b>fail-closed</b>
/// (voir <see cref="CenterLink.CenterLinkAuthenticator"/>) : un secret vide/null donne un service qui refuse tout lien.
/// </summary>
public static class CenterLinkAuthServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre <see cref="ICenterLinkAuthenticator"/> (singleton) en résolvant le secret partagé au moment de la
    /// construction via <paramref name="sharedSecretAccessor"/> — Main choisit la source (p. ex.
    /// <c>IOptions&lt;CenterServerOptions&gt;.Value.SharedSecret</c> côté Center, ou <c>IConfiguration</c> côté client).
    /// </summary>
    public static IServiceCollection AddFenrirCenterLinkAuth(
        this IServiceCollection services,
        Func<IServiceProvider, string?> sharedSecretAccessor)
    {
        ArgumentNullException.ThrowIfNull(sharedSecretAccessor);

        services.AddSingleton<ICenterLinkAuthenticator>(sp => new CenterLinkAuthenticator(sharedSecretAccessor(sp)));
        return services;
    }

    /// <summary>
    /// Surcharge de commodité : enregistre <see cref="ICenterLinkAuthenticator"/> (singleton) avec un secret déjà
    /// résolu (p. ex. <c>configuration["Center:SharedSecret"]</c>). Secret vide/null ⇒ service fail-closed.
    /// </summary>
    public static IServiceCollection AddFenrirCenterLinkAuth(this IServiceCollection services, string? sharedSecret)
    {
        services.AddSingleton<ICenterLinkAuthenticator>(new CenterLinkAuthenticator(sharedSecret));
        return services;
    }
}
