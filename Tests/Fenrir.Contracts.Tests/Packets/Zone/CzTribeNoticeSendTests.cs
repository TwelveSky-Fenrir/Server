using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTribeNoticeSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, CzTribeNoticeSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeNoticeSend, CzTribeNoticeSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[CzTribeNoticeSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Tribe war starts at dusk.");

        var ok = CzTribeNoticeSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Tribe war starts at dusk.", packet.Content);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzTribeNoticeSend.TryRead(new byte[60], out _));
    }
}
