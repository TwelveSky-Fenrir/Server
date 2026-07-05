using System.Buffers.Binary;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClWantGiftSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=17 (9-byte inbound header) -> 8-byte payload (2 int).
        Assert.Equal(8, ClaimGiftRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[ClaimGiftRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 0x0102);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), 7);

        Assert.True(ClaimGiftRequest.TryRead(buffer, out var packet));

        Assert.Equal(0x0102, packet.Sort);
        Assert.Equal(7, packet.GiftInfoIndex);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[ClaimGiftRequest.PayloadSize - 1];

        Assert.False(ClaimGiftRequest.TryRead(buffer, out _));
    }
}
