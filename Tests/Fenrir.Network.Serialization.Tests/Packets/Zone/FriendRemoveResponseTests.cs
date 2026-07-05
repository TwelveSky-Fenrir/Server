using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcFriendDeleteRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, FriendRemoveResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FriendRemove, FriendRemoveResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<FriendRemoveResponse>(1);

        Span<byte> buffer = new byte[FriendRemoveResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(FriendRemoveResponse.PayloadSize, written);

        Assert.Equal(packet.Index, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<FriendRemoveResponse>(11);

        var expected = new byte[FriendRemoveResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[FriendRemoveResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, FriendRemoveResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Index);
    }
}
