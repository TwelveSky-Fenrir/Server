using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTribeNotifySendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, CzTribeNotifySend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeNotifySend, CzTribeNotifySend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[CzTribeNotifySend.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Scroll-based tribe notice");

        var ok = CzTribeNotifySend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Scroll-based tribe notice", packet.Content);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzTribeNotifySend.TryRead(new byte[60], out _));
    }
}
