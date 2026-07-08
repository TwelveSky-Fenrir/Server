using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzTradeAskSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, TradeInviteRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TradeInvite, TradeInviteRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[TradeInviteRequest.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Nm0A");

        var ok = TradeInviteRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal("Nm0A", packet.AvatarName);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(TradeInviteRequest.TryRead(new byte[12], out _));
    }
}
