using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTradeCancelSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CzTradeCancelSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TradeCancelSend, CzTradeCancelSend.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = CzTradeCancelSend.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new CzTradeCancelSend(), packet);
    }
}
