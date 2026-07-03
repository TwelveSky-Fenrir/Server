using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTradeStartSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CzTradeStartSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TradeStartSend, CzTradeStartSend.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = CzTradeStartSend.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new CzTradeStartSend(), packet);
    }
}
