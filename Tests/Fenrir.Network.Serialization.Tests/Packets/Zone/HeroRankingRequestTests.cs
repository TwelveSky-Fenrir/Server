using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzHeroRankInfoSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, HeroRankingRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.HeroRanking, HeroRankingRequest.Opcode);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(HeroRankingRequest.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(HeroRankingRequest.TryRead(buffer, out _));
    }
}
