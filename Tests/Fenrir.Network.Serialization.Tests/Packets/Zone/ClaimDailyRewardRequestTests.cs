using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzClaimRewardItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
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
