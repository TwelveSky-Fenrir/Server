using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Fenrir.Security.Abstractions;

public readonly struct CenterLinkChallenge
{
    private readonly NonceBuffer _nonce;

        internal CenterLinkChallenge(scoped ReadOnlySpan<byte> nonce)
    {
        NonceBuffer buffer = default;
        nonce[..CenterLinkAuth.NonceSize].CopyTo(buffer);
        _nonce = buffer;
    }

        [UnscopedRef]
    public ReadOnlySpan<byte> Nonce => _nonce;
}

[InlineArray(CenterLinkAuth.NonceSize)]
internal struct NonceBuffer
{
    private byte _element0;
}
