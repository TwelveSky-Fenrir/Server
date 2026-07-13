using System.Security.Cryptography;
using System.Text;
using Fenrir.Security.Abstractions;

namespace Fenrir.Security.CenterLink;

/// <summary>
/// Implémentation HMAC-SHA256 de <see cref="ICenterLinkAuthenticator"/>. Sans état (aucun cache de nonce),
/// thread-safe, AOT-safe (aucune réflexion) et sans allocation sur les chemins chauds (assemblage sur la pile +
/// API statiques <c>HMACSHA256.HashData</c>). Le secret partagé n'est jamais journalisé ni exposé : seule sa
/// dérivation en clé HMAC est conservée en mémoire, et l'égalité des MAC est comparée en temps constant.
/// </summary>
public sealed class CenterLinkAuthenticator : ICenterLinkAuthenticator
{
    private readonly byte[]? _key;

    /// <summary>
    /// Construit l'authentificateur à partir du secret partagé (env <c>Center__SharedSecret</c>). Secret vide/null ⇒
    /// <see cref="IsEnabled"/> = <c>false</c> (fail-closed : aucun lien authentifiable). Le secret n'est jamais
    /// conservé en clair ni journalisé — seule la clé HMAC dérivée (UTF-8) est gardée.
    /// </summary>
    /// <param name="sharedSecret">Le secret partagé lu depuis <c>Center__SharedSecret</c> ; <c>null</c>/vide accepté.</param>
    public CenterLinkAuthenticator(string? sharedSecret)
    {
        _key = string.IsNullOrEmpty(sharedSecret) ? null : Encoding.UTF8.GetBytes(sharedSecret);
    }

    /// <inheritdoc />
    public bool IsEnabled => _key is not null;

    /// <inheritdoc />
    public CenterLinkChallenge IssueChallenge()
    {
        ThrowIfDisabled();

        Span<byte> nonce = stackalloc byte[CenterLinkAuth.NonceSize];
        RandomNumberGenerator.Fill(nonce);
        return new CenterLinkChallenge(nonce);
    }

    /// <inheritdoc />
    public bool VerifyHelloMac(in CenterLinkChallenge challenge, ReadOnlySpan<byte> context, ReadOnlySpan<byte> clientMac)
    {
        // Backstop fail-closed : la vérification serveur ne lève JAMAIS, quel que soit l'input du pair — un secret
        // absent, un MAC de mauvaise taille ou un contexte aberrant se traduisent par un refus silencieux.
        if (_key is null)
            return false;
        if (clientMac.Length != CenterLinkAuth.MacSize)
            return false;
        if (context.Length > CenterLinkAuth.MaxContextLength)
            return false;

        Span<byte> expected = stackalloc byte[CenterLinkAuth.MacSize];
        ComputeMac(_key, challenge.Nonce, context, expected);
        return CryptographicOperations.FixedTimeEquals(expected, clientMac);
    }

    /// <inheritdoc />
    public int ComputeHelloMac(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> context, Span<byte> destination)
    {
        ThrowIfDisabled();

        if (nonce.Length != CenterLinkAuth.NonceSize)
            throw new ArgumentException($"Nonce must be exactly {CenterLinkAuth.NonceSize} bytes.", nameof(nonce));
        if (context.Length > CenterLinkAuth.MaxContextLength)
            throw new ArgumentException($"Context must not exceed {CenterLinkAuth.MaxContextLength} bytes.", nameof(context));
        if (destination.Length < CenterLinkAuth.MacSize)
            throw new ArgumentException($"Destination must be at least {CenterLinkAuth.MacSize} bytes.", nameof(destination));

        return ComputeMac(_key!, nonce, context, destination);
    }

    private static int ComputeMac(byte[] key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> context, Span<byte> destination)
    {
        // message = nonce ‖ context, assemblé sur la pile (borné par NonceSize + MaxContextLength).
        Span<byte> message = stackalloc byte[CenterLinkAuth.NonceSize + CenterLinkAuth.MaxContextLength];
        nonce.CopyTo(message);
        context.CopyTo(message[nonce.Length..]);
        var messageLength = nonce.Length + context.Length;

        return HMACSHA256.HashData(key, message[..messageLength], destination);
    }

    private void ThrowIfDisabled()
    {
        if (_key is null)
            throw new InvalidOperationException(
                "CenterLink authentication is disabled: no shared secret configured (Center__SharedSecret). " +
                "Fail-closed: no S2S link can be authenticated.");
    }
}
