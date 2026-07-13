using Fenrir.Core.Wire;

namespace Fenrir.Security.Abstractions;

/// <summary>
/// Limiteur de débit par session <b>et par classe d'opcode</b> (token-bucket), consommé <b>inline</b> dans la
/// boucle de session avant le handler (home unique consolidé — correctif F5, ex-<c>Network.Dispatch/RateLimiting</c>
/// + <c>Login.Domain/RateLimiting</c>). Dépassement ⇒ throttle silencieux (mouvement) ou déconnexion (auth).
/// </summary>
public interface ISessionRateLimiter
{
    /// <summary>Tente de consommer un jeton pour <paramref name="opcode"/> ; <c>false</c> = quota dépassé.</summary>
    bool TryConsume(long sessionId, FenrirServer server, byte opcode);

    /// <summary>Libère l'état de limitation d'une session terminée.</summary>
    void Remove(long sessionId);
}
