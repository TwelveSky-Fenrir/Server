using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcDuelAnswerRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcDuelAnswerRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DuelAnswerRecv, ZcDuelAnswerRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcDuelAnswerRecv>(1);

        Span<byte> buffer = new byte[ZcDuelAnswerRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcDuelAnswerRecv.PayloadSize, written);

        Assert.Equal(packet.Answer, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcDuelAnswerRecv>(11);

        var expected = new byte[ZcDuelAnswerRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcDuelAnswerRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcDuelAnswerRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Answer);
    }
}
