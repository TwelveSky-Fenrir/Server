using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTeacherAnswerRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, MentorAnswerResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.MentorAnswer, MentorAnswerResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<MentorAnswerResponse>(1);

        Span<byte> buffer = new byte[MentorAnswerResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(MentorAnswerResponse.PayloadSize, written);

        Assert.Equal(packet.Answer, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<MentorAnswerResponse>(11);

        var expected = new byte[MentorAnswerResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[MentorAnswerResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, MentorAnswerResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Answer);
    }
}
