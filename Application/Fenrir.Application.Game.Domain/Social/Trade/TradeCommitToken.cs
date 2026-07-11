namespace Fenrir.Application.Game.Domain.Social.Trade;

public static class TradeCommitToken
{

        public static Guid NewForCommit()
    {
        return Guid.NewGuid();
    }
}
