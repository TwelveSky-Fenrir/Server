using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClGiftInfoSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        Assert.Equal(0, GiftListRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(GiftListRequest.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(GiftListRequest.TryRead(buffer, out _));
    }
}
