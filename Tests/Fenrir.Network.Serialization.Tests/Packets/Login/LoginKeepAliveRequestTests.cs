using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClClientOkForLoginSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
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
