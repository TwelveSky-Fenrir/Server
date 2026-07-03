using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzClaimRewardItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        // ExpectedSize=9 (inbound header only) -> 0-byte payload: empty struct.
        Assert.Equal(0, CzClaimRewardItemSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ClaimRewardItemSend, CzClaimRewardItemSend.Opcode);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(CzClaimRewardItemSend.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(CzClaimRewardItemSend.TryRead(buffer, out _));
    }
}
