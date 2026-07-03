using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGetRewardItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        // ExpectedSize=9 (inbound header only) -> 0-byte payload: empty struct.
        Assert.Equal(0, GetDailyRewardCatalogRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GetDailyRewardCatalog, GetDailyRewardCatalogRequest.Opcode);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(GetDailyRewardCatalogRequest.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(GetDailyRewardCatalogRequest.TryRead(buffer, out _));
    }
}
