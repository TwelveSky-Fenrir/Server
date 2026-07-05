using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcFriendAnswerRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, FriendAnswerResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FriendAnswer, FriendAnswerResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<FriendAnswerResponse>(1);

        Span<byte> buffer = new byte[FriendAnswerResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(FriendAnswerResponse.PayloadSize, written);

        Assert.Equal(packet.Answer, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<FriendAnswerResponse>(11);

        var expected = new byte[FriendAnswerResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[FriendAnswerResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, FriendAnswerResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Answer);
    }
}
