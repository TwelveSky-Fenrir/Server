using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcDuelTimeInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, DuelCountdownResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DuelCountdown, DuelCountdownResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<DuelCountdownResponse>(1);

        Span<byte> buffer = new byte[DuelCountdownResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(DuelCountdownResponse.PayloadSize, written);

        Assert.Equal(packet.RemainTime, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<DuelCountdownResponse>(11);

        var expected = new byte[DuelCountdownResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[DuelCountdownResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, DuelCountdownResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.RemainTime);
    }
}
