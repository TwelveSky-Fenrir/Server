using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class ClDemandZoneServerInfoSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=13 (9-byte inbound header) -> 4-byte payload (1 int).
        Assert.Equal(4, ClDemandZoneServerInfoSend.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[ClDemandZoneServerInfoSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 12);

        Assert.True(ClDemandZoneServerInfoSend.TryRead(buffer, out var packet));
        Assert.Equal(12, packet.AvatarPost);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[ClDemandZoneServerInfoSend.PayloadSize - 1];

        Assert.False(ClDemandZoneServerInfoSend.TryRead(buffer, out _));
    }
}
