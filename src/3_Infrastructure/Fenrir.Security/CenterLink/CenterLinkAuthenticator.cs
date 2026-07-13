using System.Security.Cryptography;
using System.Text;
using Fenrir.Security.Abstractions;

namespace Fenrir.Security.CenterLink;

public sealed class CenterLinkAuthenticator : ICenterLinkAuthenticator
{
    private readonly byte[]? _key;

        public CenterLinkAuthenticator(string? sharedSecret)
    {
        _key = string.IsNullOrEmpty(sharedSecret) ? null : Encoding.UTF8.GetBytes(sharedSecret);
    }

        public bool IsEnabled => _key is not null;

        public CenterLinkChallenge IssueChallenge()
    {
        ThrowIfDisabled();

        Span<byte> nonce = stackalloc byte[CenterLinkAuth.NonceSize];
        RandomNumberGenerator.Fill(nonce);
        return new CenterLinkChallenge(nonce);
    }

        public bool VerifyHelloMac(in CenterLinkChallenge challenge, ReadOnlySpan<byte> context, ReadOnlySpan<byte> clientMac)
    {
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
