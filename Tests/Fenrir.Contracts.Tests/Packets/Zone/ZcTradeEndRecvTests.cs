using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTradeEndRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcTradeEndRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TradeEndRecv, ZcTradeEndRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcTradeEndRecv>(1);

        Span<byte> buffer = new byte[ZcTradeEndRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcTradeEndRecv.PayloadSize, written);

        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcTradeEndRecv>(11);

        var expected = new byte[ZcTradeEndRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcTradeEndRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcTradeEndRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Result);
    }
}
