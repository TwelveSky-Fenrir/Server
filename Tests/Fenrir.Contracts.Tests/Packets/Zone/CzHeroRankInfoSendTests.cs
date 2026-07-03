using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzHeroRankInfoSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CzHeroRankInfoSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.HeroRankInfoSend, CzHeroRankInfoSend.Opcode);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(CzHeroRankInfoSend.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(CzHeroRankInfoSend.TryRead(buffer, out _));
    }
}
