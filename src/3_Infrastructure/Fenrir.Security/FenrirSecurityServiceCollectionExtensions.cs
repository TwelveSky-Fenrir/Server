using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Security;

/// <summary>
/// Enregistrement DI du module <c>Fenrir.Security</c> (anti-flood/rate-limit/firewall unifiés — correctif F5).
/// À appeler <b>après</b> <c>AddFenrirData()</c> : <see cref="ApplicationFirewall"/> compose les 3 interfaces
/// repo Security enregistrées par <c>AddFenrirData</c> (l'invariant « Data isolé » interdit à Data de connaître
/// ce type first-class Security). Le rate-limit par session (<c>SessionRateLimiter</c>) et par IP
/// (<c>LoginIpRateLimiter</c>) restent enregistrés au plus près de leurs consommateurs (composition roots).
/// </summary>
public static class FenrirSecurityServiceCollectionExtensions
{
    /// <summary>Enregistre les services in-process de Security (firewall d'application composé sur les repos Data).</summary>
    public static IServiceCollection AddFenrirSecurity(this IServiceCollection services)
    {
        services.AddSingleton<ApplicationFirewall>();
        return services;
    }
}
