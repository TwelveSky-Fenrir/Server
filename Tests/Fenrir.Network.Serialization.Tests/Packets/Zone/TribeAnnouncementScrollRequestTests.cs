using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzTribeNotifySendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, TribeAnnouncementScrollRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeAnnouncementScroll, TribeAnnouncementScrollRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[TribeAnnouncementScrollRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Scroll-based tribe notice");

        var ok = TribeAnnouncementScrollRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Scroll-based tribe notice", packet.Content);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(TribeAnnouncementScrollRequest.TryRead(new byte[60], out _));
    }
}
