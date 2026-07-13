using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Fenrir.Security.Abstractions;

/// <summary>
/// Défi (nonce) à usage unique émis par le serveur (Center) au moment de l'accept, puis conservé dans l'état de la
/// connexion S2S le temps de vérifier la réponse HMAC du pair. Type valeur à tampon inline (zéro-alloc) : à stocker
/// <b>par valeur</b> dans l'état de session, en sérialiser <see cref="Nonce"/> sur le fil, puis à repasser par
/// <c>in</c> à <see cref="ICenterLinkAuthenticator.VerifyHelloMac"/>. Seul l'authentificateur peut en produire un
/// (constructeur interne) : un pair ne fabrique jamais son propre défi.
/// </summary>
public readonly struct CenterLinkChallenge
{
    private readonly NonceBuffer _nonce;

    /// <summary>
    /// Copie <paramref name="nonce"/> (exactement <see cref="CenterLinkAuth.NonceSize"/> octets) dans le défi.
    /// </summary>
    internal CenterLinkChallenge(scoped ReadOnlySpan<byte> nonce)
    {
        NonceBuffer buffer = default;
        nonce[..CenterLinkAuth.NonceSize].CopyTo(buffer);
        _nonce = buffer;
    }

    /// <summary>
    /// Octets du nonce à transmettre au client. À lire depuis un défi <b>stocké</b> (une variable/champ de durée de
    /// vie suffisante), jamais depuis une valeur temporaire — la tranche pointe dans le stockage du défi.
    /// </summary>
    /// <remarks><see cref="UnscopedRefAttribute"/> : la tranche peut s'échapper de l'accesseur (elle vise le tampon
    /// inline de <c>this</c>) — sûr tant qu'elle est lue depuis un défi stocké, l'usage documenté.</remarks>
    [UnscopedRef]
    public ReadOnlySpan<byte> Nonce => _nonce;
}

/// <summary>Tampon inline de <see cref="CenterLinkAuth.NonceSize"/> octets adossant <see cref="CenterLinkChallenge"/>.</summary>
[InlineArray(CenterLinkAuth.NonceSize)]
internal struct NonceBuffer
{
    private byte _element0;
}
