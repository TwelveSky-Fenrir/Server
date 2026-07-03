using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTeacherStartRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(17, ZcTeacherStartRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TeacherStartRecv, ZcTeacherStartRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcTeacherStartRecv>(1);

        Span<byte> buffer = new byte[ZcTeacherStartRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcTeacherStartRecv.PayloadSize, written);

        Assert.Equal(packet.Sort, BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)));
        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(4, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcTeacherStartRecv>(11);

        var expected = new byte[ZcTeacherStartRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcTeacherStartRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcTeacherStartRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..], value.Sort);
        WireTestKit.WriteFixedString(destination.Slice(4, 13), value.AvatarName);
    }
}
