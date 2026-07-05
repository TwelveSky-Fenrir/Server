using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTradeMenuSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, TradeLockRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TradeLock, TradeLockRequest.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = TradeLockRequest.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new TradeLockRequest(), packet);
    }
}
