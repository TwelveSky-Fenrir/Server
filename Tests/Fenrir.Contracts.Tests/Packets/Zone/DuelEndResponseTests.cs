using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcDuelEndRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, DuelEndResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DuelEnd, DuelEndResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<DuelEndResponse>(1);

        Span<byte> buffer = new byte[DuelEndResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(DuelEndResponse.PayloadSize, written);

        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<DuelEndResponse>(11);

        var expected = new byte[DuelEndResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[DuelEndResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, DuelEndResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Result);
    }
}
