using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcFriendMakeRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(17, FriendAddResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FriendAdd, FriendAddResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<FriendAddResponse>(1);

        Span<byte> buffer = new byte[FriendAddResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(FriendAddResponse.PayloadSize, written);

        Assert.Equal(packet.Index, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(4, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<FriendAddResponse>(11);

        var expected = new byte[FriendAddResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[FriendAddResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, FriendAddResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Index);
        WireTestKit.WriteFixedString(destination.Slice(4, 13), value.AvatarName);
    }
}
