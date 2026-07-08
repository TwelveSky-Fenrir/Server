using System.Buffers.Binary;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClDeleteAvatarSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=21 (9-byte inbound header) -> 12-byte payload (3 int).
        Assert.Equal(12, DeleteAvatarRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[DeleteAvatarRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), 0x1122);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8, 4), 0x3344);

        Assert.True(DeleteAvatarRequest.TryRead(buffer, out var packet));

        Assert.Equal(4, packet.AvatarPost);
        Assert.Equal(0x1122, packet.Unknow1);
        Assert.Equal(0x3344, packet.Unknow2);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[DeleteAvatarRequest.PayloadSize - 1];

        Assert.False(DeleteAvatarRequest.TryRead(buffer, out _));
    }
}
