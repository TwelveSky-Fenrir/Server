using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class ClFailMoveZone1SendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=9 (inbound header only) -> 0-byte payload: empty struct (CLIENT.h l.107-109).
        Assert.Equal(0, ClFailMoveZone1Send.PayloadSize);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(ClFailMoveZone1Send.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(ClFailMoveZone1Send.TryRead(buffer, out _));
    }
}
