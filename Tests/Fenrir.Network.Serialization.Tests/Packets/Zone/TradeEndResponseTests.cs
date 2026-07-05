using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTradeEndRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, TradeEndResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TradeEnd, TradeEndResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<TradeEndResponse>(1);

        Span<byte> buffer = new byte[TradeEndResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(TradeEndResponse.PayloadSize, written);

        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<TradeEndResponse>(11);

        var expected = new byte[TradeEndResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[TradeEndResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, TradeEndResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Result);
    }
}
