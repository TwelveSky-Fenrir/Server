using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzFailMoveZone2SendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        Assert.Equal(0, ZoneTransferCancelRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ZoneTransferCancel, ZoneTransferCancelRequest.Opcode);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(ZoneTransferCancelRequest.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(ZoneTransferCancelRequest.TryRead(buffer, out _));
    }
}
