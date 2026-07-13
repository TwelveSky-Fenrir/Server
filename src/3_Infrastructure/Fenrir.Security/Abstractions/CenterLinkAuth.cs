namespace Fenrir.Security.Abstractions;

public static class CenterLinkAuth
{
    public const int NonceSize = 32;

    public const int MacSize = 32;

    public const int MaxContextLength = 256;

    /// <summary>Longueur minimale (caractères) exigée d'un secret partagé NON vide. Un secret plus court est refusé
    /// à la construction (fail-fast sur configuration faible) : un secret court affaiblit la clé HMAC. Un secret
    /// vide/absent reste géré séparément (fail-closed : lien désactivé, aucun handshake authentifiable).</summary>
    public const int MinSecretLength = 16;
}
