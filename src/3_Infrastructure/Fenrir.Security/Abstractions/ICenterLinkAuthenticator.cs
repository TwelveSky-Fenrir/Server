namespace Fenrir.Security.Abstractions;

public interface ICenterLinkAuthenticator
{

        bool IsEnabled { get; }

        CenterLinkChallenge IssueChallenge();

        bool VerifyHelloMac(in CenterLinkChallenge challenge, ReadOnlySpan<byte> context, ReadOnlySpan<byte> clientMac);

        int ComputeHelloMac(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> context, Span<byte> destination);
}
