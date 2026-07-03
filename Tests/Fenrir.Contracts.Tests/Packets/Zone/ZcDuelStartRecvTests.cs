using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcDuelStartRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, ZcDuelStartRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DuelStartRecv, ZcDuelStartRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcDuelStartRecv>(1);

        Span<byte> buffer = new byte[ZcDuelStartRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcDuelStartRecv.PayloadSize, written);

        for (var i = 0; i < 3; i++)
            Assert.Equal(packet.DuelState[i], BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(i * 4, 4)));
        Assert.Equal(packet.RemainTime, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(12, 4)));
        Assert.Equal(packet.EatDrugState, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(16, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcDuelStartRecv>(11);

        var expected = new byte[ZcDuelStartRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcDuelStartRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcDuelStartRecv value)
    {
        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(i * 4)..], value.DuelState[i]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.RemainTime);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.EatDrugState);
    }
}
