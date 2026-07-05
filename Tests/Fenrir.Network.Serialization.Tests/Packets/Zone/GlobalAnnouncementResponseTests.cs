using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGeneralNoticeRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, GlobalAnnouncementResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GlobalAnnouncement, GlobalAnnouncementResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new GlobalAnnouncementResponse { Content = "Rare item crafted!" };

        Span<byte> buffer = new byte[GlobalAnnouncementResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GlobalAnnouncementResponse.PayloadSize, written);

        var golden = new byte[GlobalAnnouncementResponse.PayloadSize];
        WireTestKit.WriteFixedString(golden, "Rare item crafted!");

        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var packet = new GlobalAnnouncementResponse { Content = "System notice" };

        Span<byte> buffer = new byte[GlobalAnnouncementResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal("System notice", WireTestKit.ReadFixedString(buffer));
    }
}
