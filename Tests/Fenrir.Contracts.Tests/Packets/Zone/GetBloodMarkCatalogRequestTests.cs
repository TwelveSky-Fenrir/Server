using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzDemandBloodMarkSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        // ExpectedSize=9 (inbound header only) -> 0-byte payload: empty struct.
        Assert.Equal(0, GetBloodMarkCatalogRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GetBloodMarkCatalog, GetBloodMarkCatalogRequest.Opcode);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(GetBloodMarkCatalogRequest.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(GetBloodMarkCatalogRequest.TryRead(buffer, out _));
    }
}
