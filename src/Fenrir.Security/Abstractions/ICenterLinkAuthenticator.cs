namespace Fenrir.Security.Abstractions;

public interface ICenterLinkAuthenticator
{
    public bool IsEnabled { get; }

    public CenterLinkChallenge IssueChallenge();

    public bool VerifyHelloMac(in CenterLinkChallenge challenge, ReadOnlySpan<byte> context,
        ReadOnlySpan<byte> clientMac);

    public int ComputeHelloMac(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> context, Span<byte> destination);
}
