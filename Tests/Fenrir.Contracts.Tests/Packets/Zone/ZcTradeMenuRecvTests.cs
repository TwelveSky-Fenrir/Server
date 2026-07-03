using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTradeMenuRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcTradeMenuRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TradeMenuRecv, ZcTradeMenuRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcTradeMenuRecv>(1);

        Span<byte> buffer = new byte[ZcTradeMenuRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcTradeMenuRecv.PayloadSize, written);

        Assert.Equal(packet.CheckMe, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcTradeMenuRecv>(11);

        var expected = new byte[ZcTradeMenuRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcTradeMenuRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcTradeMenuRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.CheckMe);
    }
}
