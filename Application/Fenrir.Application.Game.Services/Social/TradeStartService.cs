using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeStartService(TradeRegistry trades, ILogger<TradeStartService> logger) : ITradeStartService
{
    public TradeStartResult Start(int callerId)
    {
        if (!trades.TryStart(callerId, out var trade))
        {
            logger.LogDebug("Trade start ignored: character {CallerId} has no accepted negotiation to start",
                callerId);
            return new TradeStartResult(false, null);
        }

        logger.LogDebug("Trade session started: character {PlayerAId} <-> character {PlayerBId}", trade.PlayerAId,
            trade.PlayerBId);
        return new TradeStartResult(true, trade);
    }

        public void AbortStart(int callerId)
    {
        if (trades.TryAbortStartForCaller(callerId))
            logger.LogDebug(
                "Trade start rolled back: character {CallerId}'s post-commit partner re-validation failed; " +
                "caller reset to idle, partner state left untouched", callerId);
    }
}
