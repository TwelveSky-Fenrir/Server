using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcDuelEndRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcDuelEndRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DuelEndRecv, ZcDuelEndRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcDuelEndRecv>(1);

        Span<byte> buffer = new byte[ZcDuelEndRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcDuelEndRecv.PayloadSize, written);

        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcDuelEndRecv>(11);

        var expected = new byte[ZcDuelEndRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcDuelEndRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcDuelEndRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Result);
    }
}
