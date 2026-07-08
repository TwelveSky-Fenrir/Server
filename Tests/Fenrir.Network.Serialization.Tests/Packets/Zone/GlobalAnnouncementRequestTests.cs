using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzGeneralNoticeSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, GlobalAnnouncementRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GlobalAnnouncement, GlobalAnnouncementRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[GlobalAnnouncementRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Server maintenance in 5 minutes.");

        var ok = GlobalAnnouncementRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Server maintenance in 5 minutes.", packet.Content);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GlobalAnnouncementRequest.TryRead(new byte[60], out _));
    }
}
