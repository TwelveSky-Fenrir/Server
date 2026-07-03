using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzFailMoveZone2SendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=9 (inbound header only) -> 0-byte payload: empty struct (CLIENT.h:155-161).
        Assert.Equal(0, CzFailMoveZone2Send.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FailMoveZone2Send, CzFailMoveZone2Send.Opcode);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(CzFailMoveZone2Send.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(CzFailMoveZone2Send.TryRead(buffer, out _));
    }
}
