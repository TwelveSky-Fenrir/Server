using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcFriendAnswerRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcFriendAnswerRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FriendAnswerRecv, ZcFriendAnswerRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcFriendAnswerRecv>(1);

        Span<byte> buffer = new byte[ZcFriendAnswerRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcFriendAnswerRecv.PayloadSize, written);

        Assert.Equal(packet.Answer, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcFriendAnswerRecv>(11);

        var expected = new byte[ZcFriendAnswerRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcFriendAnswerRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcFriendAnswerRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Answer);
    }
}
