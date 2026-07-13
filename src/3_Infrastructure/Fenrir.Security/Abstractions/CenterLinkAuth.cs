namespace Fenrir.Security.Abstractions;

/// <summary>
/// Constantes de format du handshake CenterLink (lien serveur-à-serveur authentifié par défi-réponse HMAC-SHA256).
/// Publiques pour dimensionner sans allocation les tampons wire aux deux extrémités : côté serveur (Center, qui émet
/// le nonce et vérifie la réponse) et côté client (Login/Game, qui calcule la réponse).
/// </summary>
public static class CenterLinkAuth
{
    /// <summary>
    /// Taille du nonce (défi) en octets — 256 bits, à usage unique par handshake. Bien au-delà du plancher de
    /// 16 octets exigé pour un défi anti-rejeu.
    /// </summary>
    public const int NonceSize = 32;

    /// <summary>Taille de la réponse HMAC-SHA256 en octets (256 bits) — sortie fixe de l'algorithme.</summary>
    public const int MacSize = 32;

    /// <summary>
    /// Longueur maximale (octets) du contexte de liaison optionnel replié dans le MAC. Borne le tampon pile
    /// d'assemblage <c>nonce ‖ context</c> et sert de garde fail-closed contre un contexte aberrant.
    /// </summary>
    public const int MaxContextLength = 256;
}
