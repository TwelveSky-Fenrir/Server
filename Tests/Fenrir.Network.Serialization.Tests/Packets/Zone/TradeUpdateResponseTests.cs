using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTradeStateRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(232, TradeUpdateResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TradeUpdate, TradeUpdateResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<TradeUpdateResponse>(1);

        Span<byte> buffer = new byte[TradeUpdateResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(TradeUpdateResponse.PayloadSize, written);

        Assert.Equal(packet.TradeMoney, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
        for (var i = 0; i < 32; i++)
            Assert.Equal(packet.Trade[i], BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(4 + i * 4, 4)));
        for (var i = 0; i < 24; i++)
            Assert.Equal(packet.TradeSocket[i], BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(132 + i * 4, 4)));
        Assert.Equal(packet.BigTradeMoney, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(228, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<TradeUpdateResponse>(11);

        var expected = new byte[TradeUpdateResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[TradeUpdateResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, TradeUpdateResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.TradeMoney);
        for (var i = 0; i < 32; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(4 + i * 4)..], value.Trade[i]);
        for (var i = 0; i < 24; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(132 + i * 4)..], value.TradeSocket[i]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[228..], value.BigTradeMoney);
    }
}
