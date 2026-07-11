using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcUpdateCashItemInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CashCatalogInvalidatedResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.CashCatalogInvalidated, CashCatalogInvalidatedResponse.Opcode);
    }

    [Fact]
    public void Write_EmptyPayload_WritesZeroBytes()
    {
        var packet = new CashCatalogInvalidatedResponse();

        var written = packet.Write(Span<byte>.Empty);

        Assert.Equal(0, written);
    }
}
