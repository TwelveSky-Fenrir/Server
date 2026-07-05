using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGuildNoticeRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(74, GuildAnnouncementResponse.PayloadSize);
        Assert.Equal(13 + 61, GuildAnnouncementResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildAnnouncement, GuildAnnouncementResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new GuildAnnouncementResponse { AvatarName = "Odin", Content = "New guild policy in effect." };

        Span<byte> buffer = new byte[GuildAnnouncementResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GuildAnnouncementResponse.PayloadSize, written);

        var golden = new byte[GuildAnnouncementResponse.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(13, 61), "New guild policy in effect.");

        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var packet = new GuildAnnouncementResponse { AvatarName = "Thor", Content = "Meeting tonight" };

        Span<byte> buffer = new byte[GuildAnnouncementResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal("Thor", WireTestKit.ReadFixedString(buffer[..13]));
        Assert.Equal("Meeting tonight", WireTestKit.ReadFixedString(buffer[13..]));
    }
}
