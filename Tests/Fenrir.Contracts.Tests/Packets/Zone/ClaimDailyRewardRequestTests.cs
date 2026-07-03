using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzClaimRewardItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        // ExpectedSize=9 (inbound header only) -> 0-byte payload: empty struct.
        Assert.Equal(0, ClaimDailyRewardRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ClaimDailyReward, ClaimDailyRewardRequest.Opcode);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(ClaimDailyRewardRequest.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(ClaimDailyRewardRequest.TryRead(buffer, out _));
    }
}
