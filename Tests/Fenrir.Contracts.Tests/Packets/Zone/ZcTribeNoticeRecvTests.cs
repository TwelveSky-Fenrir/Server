using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTribeNoticeRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(78, ZcTribeNoticeRecv.PayloadSize);
        Assert.Equal(4 + 13 + 61, ZcTribeNoticeRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeNoticeRecv, ZcTribeNoticeRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcTribeNoticeRecv { TribeRole = 1, AvatarName = "Odin", Content = "Attack at dawn." };

        Span<byte> buffer = new byte[ZcTribeNoticeRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcTribeNoticeRecv.PayloadSize, written);

        var golden = new byte[ZcTribeNoticeRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 1);
        WireTestKit.WriteFixedString(golden.AsSpan(4, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(17, 61), "Attack at dawn.");

        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var packet = new ZcTribeNoticeRecv { TribeRole = 2, AvatarName = "Freya", Content = "Vice-master notice" };

        Span<byte> buffer = new byte[ZcTribeNoticeRecv.PayloadSize];
        packet.Write(buffer);

        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal("Freya", WireTestKit.ReadFixedString(buffer.Slice(4, 13)));
        Assert.Equal("Vice-master notice", WireTestKit.ReadFixedString(buffer[17..]));
    }
}
