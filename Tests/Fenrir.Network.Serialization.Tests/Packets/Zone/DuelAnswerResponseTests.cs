using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcDuelAnswerRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, DuelAnswerResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DuelAnswer, DuelAnswerResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<DuelAnswerResponse>(1);

        Span<byte> buffer = new byte[DuelAnswerResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(DuelAnswerResponse.PayloadSize, written);

        Assert.Equal(packet.Answer, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<DuelAnswerResponse>(11);

        var expected = new byte[DuelAnswerResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[DuelAnswerResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, DuelAnswerResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Answer);
    }
}
