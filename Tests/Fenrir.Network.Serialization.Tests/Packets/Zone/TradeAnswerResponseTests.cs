using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTradeAnswerRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, TradeAnswerResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TradeAnswer, TradeAnswerResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<TradeAnswerResponse>(1);

        Span<byte> buffer = new byte[TradeAnswerResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(TradeAnswerResponse.PayloadSize, written);

        Assert.Equal(packet.Answer, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<TradeAnswerResponse>(11);

        var expected = new byte[TradeAnswerResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[TradeAnswerResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, TradeAnswerResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Answer);
    }
}
