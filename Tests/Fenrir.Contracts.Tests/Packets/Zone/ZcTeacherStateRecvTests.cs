using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTeacherStateRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcTeacherStateRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TeacherStateRecv, ZcTeacherStateRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcTeacherStateRecv>(1);

        Span<byte> buffer = new byte[ZcTeacherStateRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcTeacherStateRecv.PayloadSize, written);

        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcTeacherStateRecv>(11);

        var expected = new byte[ZcTeacherStateRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcTeacherStateRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcTeacherStateRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Result);
    }
}
