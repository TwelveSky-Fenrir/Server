namespace Fenrir.Security.Abstractions;

/// <summary>
/// Port d'éjection consommé par l'anti-flood : abat toutes les sessions d'une IP au moment du block.
/// Implémenté par le <c>SessionRegistry</c> de la couche transport (<c>Fenrir.Network</c>) et injecté dans
/// <c>IpFloodGuard</c> — c'est ce qui évite l'arête interdite <c>Security → Network</c> (miroir du délégué
/// <c>blockIpAsync</c> déjà découplé vers Data). Synchrone : <c>ClientSession.Abort</c> est non-bloquant.
/// </summary>
public interface IFloodKickSink
{
    /// <summary>Éjecte toutes les sessions de <paramref name="ipAddress"/> ; retourne le nombre abattu.</summary>
    int KickByRemoteAddress(string ipAddress);
}
