using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClFailMoveZone1SendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=9 (header only) -> 0-byte payload: empty struct (CLIENT.h).
        Assert.Equal(0, ZoneTransferFailureRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(ZoneTransferFailureRequest.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(ZoneTransferFailureRequest.TryRead(buffer, out _));
    }
}
