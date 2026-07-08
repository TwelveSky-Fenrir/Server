using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcFriendFindRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, FriendLocateResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FriendLocate, FriendLocateResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<FriendLocateResponse>(1);

        Span<byte> buffer = new byte[FriendLocateResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(FriendLocateResponse.PayloadSize, written);

        Assert.Equal(packet.Index, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
        Assert.Equal(packet.ZoneNumber, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(4, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<FriendLocateResponse>(11);

        var expected = new byte[FriendLocateResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[FriendLocateResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, FriendLocateResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Index);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.ZoneNumber);
    }
}
