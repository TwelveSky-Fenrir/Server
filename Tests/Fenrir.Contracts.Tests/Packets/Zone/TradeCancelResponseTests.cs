using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTradeCancelRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, TradeCancelResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TradeCancel, TradeCancelResponse.Opcode);
    }

    [Fact]
    public void Write_EmptyPayload_ReturnsZero()
    {
        var packet = new TradeCancelResponse();

        Span<byte> buffer = [];
        var written = packet.Write(buffer);

        Assert.Equal(0, written);
    }
}
