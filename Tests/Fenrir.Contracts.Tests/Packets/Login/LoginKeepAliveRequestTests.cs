using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class ClClientOkForLoginSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=9 (inbound header only) -> 0-byte payload: empty struct.
        Assert.Equal(0, LoginKeepAliveRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(LoginKeepAliveRequest.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(LoginKeepAliveRequest.TryRead(buffer, out _));
    }
}
