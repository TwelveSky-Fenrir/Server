using System.Buffers.Binary;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClDemandZoneServerInfoSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=13 (9-byte inbound header) -> 4-byte payload (1 int).
        Assert.Equal(4, ZoneTransferRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[ZoneTransferRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 12);

        Assert.True(ZoneTransferRequest.TryRead(buffer, out var packet));
        Assert.Equal(12, packet.AvatarPost);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[ZoneTransferRequest.PayloadSize - 1];

        Assert.False(ZoneTransferRequest.TryRead(buffer, out _));
    }
}
