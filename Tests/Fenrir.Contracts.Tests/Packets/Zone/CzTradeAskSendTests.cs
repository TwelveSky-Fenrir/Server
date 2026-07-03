using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTradeAskSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, CzTradeAskSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TradeAskSend, CzTradeAskSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[CzTradeAskSend.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Nm0A");

        var ok = CzTradeAskSend.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal("Nm0A", packet.AvatarName);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzTradeAskSend.TryRead(new byte[12], out _));
    }
}
