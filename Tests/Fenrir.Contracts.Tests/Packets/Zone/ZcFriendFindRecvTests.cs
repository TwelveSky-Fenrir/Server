using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcFriendFindRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, ZcFriendFindRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FriendFindRecv, ZcFriendFindRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcFriendFindRecv>(1);

        Span<byte> buffer = new byte[ZcFriendFindRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcFriendFindRecv.PayloadSize, written);

        Assert.Equal(packet.Index, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
        Assert.Equal(packet.ZoneNumber, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(4, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcFriendFindRecv>(11);

        var expected = new byte[ZcFriendFindRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcFriendFindRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcFriendFindRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Index);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.ZoneNumber);
    }
}
