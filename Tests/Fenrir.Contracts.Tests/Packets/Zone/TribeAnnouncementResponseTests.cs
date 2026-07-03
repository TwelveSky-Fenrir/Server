using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTribeNoticeRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(78, TribeAnnouncementResponse.PayloadSize);
        Assert.Equal(4 + 13 + 61, TribeAnnouncementResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeAnnouncement, TribeAnnouncementResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new TribeAnnouncementResponse { TribeRole = 1, AvatarName = "Odin", Content = "Attack at dawn." };

        Span<byte> buffer = new byte[TribeAnnouncementResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(TribeAnnouncementResponse.PayloadSize, written);

        var golden = new byte[TribeAnnouncementResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 1);
        WireTestKit.WriteFixedString(golden.AsSpan(4, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(17, 61), "Attack at dawn.");

        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var packet = new TribeAnnouncementResponse { TribeRole = 2, AvatarName = "Freya", Content = "Vice-master notice" };

        Span<byte> buffer = new byte[TribeAnnouncementResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal("Freya", WireTestKit.ReadFixedString(buffer.Slice(4, 13)));
        Assert.Equal("Vice-master notice", WireTestKit.ReadFixedString(buffer[17..]));
    }
}
