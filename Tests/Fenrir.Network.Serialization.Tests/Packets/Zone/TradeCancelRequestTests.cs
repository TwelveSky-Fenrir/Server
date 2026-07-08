using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzTradeCancelSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, TradeCancelRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TradeCancel, TradeCancelRequest.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = TradeCancelRequest.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new TradeCancelRequest(), packet);
    }
}
