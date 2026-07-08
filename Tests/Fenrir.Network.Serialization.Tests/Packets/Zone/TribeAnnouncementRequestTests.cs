using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzTribeNoticeSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, TribeAnnouncementRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeAnnouncement, TribeAnnouncementRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[TribeAnnouncementRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Tribe war starts at dusk.");

        var ok = TribeAnnouncementRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Tribe war starts at dusk.", packet.Content);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(TribeAnnouncementRequest.TryRead(new byte[60], out _));
    }
}
