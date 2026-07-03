using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTradeAskRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(17, ZcTradeAskRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TradeAskRecv, ZcTradeAskRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcTradeAskRecv>(1);

        Span<byte> buffer = new byte[ZcTradeAskRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcTradeAskRecv.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
        Assert.Equal(packet.Level, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(13, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcTradeAskRecv>(11);

        var expected = new byte[ZcTradeAskRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcTradeAskRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcTradeAskRecv value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
        BinaryPrimitives.WriteInt32LittleEndian(destination[13..], value.Level);
    }
}
