using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcDuelStartRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, DuelStartResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DuelStart, DuelStartResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<DuelStartResponse>(1);

        Span<byte> buffer = new byte[DuelStartResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(DuelStartResponse.PayloadSize, written);

        for (var i = 0; i < 3; i++)
            Assert.Equal(packet.DuelState[i], BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(i * 4, 4)));
        Assert.Equal(packet.RemainTime, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(12, 4)));
        Assert.Equal(packet.EatDrugState, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(16, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<DuelStartResponse>(11);

        var expected = new byte[DuelStartResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[DuelStartResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, DuelStartResponse value)
    {
        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(i * 4)..], value.DuelState[i]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.RemainTime);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.EatDrugState);
    }
}
