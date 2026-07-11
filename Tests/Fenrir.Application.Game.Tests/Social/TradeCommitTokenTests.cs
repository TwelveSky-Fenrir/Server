using Fenrir.Application.Game.Domain.Social.Trade;

namespace Fenrir.Application.Game.Tests.Social;

/// <summary>Covers <see cref="TradeCommitToken" /> -- the C8-trade-finalize idempotency-token factory.</summary>
public class TradeCommitTokenTests
{
    [Fact]
    public void NewForCommit_ReturnsNonEmptyGuid()
    {
        Assert.NotEqual(Guid.Empty, TradeCommitToken.NewForCommit());
    }

    [Fact]
    public void NewForCommit_TwoCalls_ReturnDifferentValues()
    {
        var first = TradeCommitToken.NewForCommit();
        var second = TradeCommitToken.NewForCommit();

        Assert.NotEqual(first, second);
    }
}
