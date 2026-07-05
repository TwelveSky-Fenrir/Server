using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGuildNoticeSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, GuildAnnouncementRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildAnnouncement, GuildAnnouncementRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[GuildAnnouncementRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Guild hall renovated!");

        var ok = GuildAnnouncementRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Guild hall renovated!", packet.Content);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GuildAnnouncementRequest.TryRead(new byte[60], out _));
    }
}
