using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcFriendDeleteRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcFriendDeleteRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FriendDeleteRecv, ZcFriendDeleteRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcFriendDeleteRecv>(1);

        Span<byte> buffer = new byte[ZcFriendDeleteRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcFriendDeleteRecv.PayloadSize, written);

        Assert.Equal(packet.Index, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcFriendDeleteRecv>(11);

        var expected = new byte[ZcFriendDeleteRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcFriendDeleteRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcFriendDeleteRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Index);
    }
}
