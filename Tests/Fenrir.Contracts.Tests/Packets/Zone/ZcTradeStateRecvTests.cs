using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTradeStateRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(232, ZcTradeStateRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TradeStateRecv, ZcTradeStateRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcTradeStateRecv>(1);

        Span<byte> buffer = new byte[ZcTradeStateRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcTradeStateRecv.PayloadSize, written);

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
        var packet = WireTestKit.CreatePopulated<ZcTradeStateRecv>(11);

        var expected = new byte[ZcTradeStateRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcTradeStateRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcTradeStateRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.TradeMoney);
        for (var i = 0; i < 32; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(4 + i * 4)..], value.Trade[i]);
        for (var i = 0; i < 24; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(132 + i * 4)..], value.TradeSocket[i]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[228..], value.BigTradeMoney);
    }
}
