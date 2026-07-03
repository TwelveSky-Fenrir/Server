using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcFriendMakeRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(17, ZcFriendMakeRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FriendMakeRecv, ZcFriendMakeRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcFriendMakeRecv>(1);

        Span<byte> buffer = new byte[ZcFriendMakeRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcFriendMakeRecv.PayloadSize, written);

        Assert.Equal(packet.Index, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(4, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcFriendMakeRecv>(11);

        var expected = new byte[ZcFriendMakeRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcFriendMakeRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcFriendMakeRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Index);
        WireTestKit.WriteFixedString(destination.Slice(4, 13), value.AvatarName);
    }
}
