using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGeneralNoticeSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, CzGeneralNoticeSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GeneralNoticeSend, CzGeneralNoticeSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[CzGeneralNoticeSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Server maintenance in 5 minutes.");

        var ok = CzGeneralNoticeSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Server maintenance in 5 minutes.", packet.Content);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzGeneralNoticeSend.TryRead(new byte[60], out _));
    }
}
