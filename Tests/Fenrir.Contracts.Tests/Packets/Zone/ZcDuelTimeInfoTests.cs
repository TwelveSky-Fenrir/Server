using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcDuelTimeInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcDuelTimeInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DuelTimeInfo, ZcDuelTimeInfo.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcDuelTimeInfo>(1);

        Span<byte> buffer = new byte[ZcDuelTimeInfo.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcDuelTimeInfo.PayloadSize, written);

        Assert.Equal(packet.RemainTime, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcDuelTimeInfo>(11);

        var expected = new byte[ZcDuelTimeInfo.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcDuelTimeInfo.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcDuelTimeInfo value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.RemainTime);
    }
}
