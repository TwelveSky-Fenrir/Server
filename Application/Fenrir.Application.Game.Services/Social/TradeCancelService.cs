using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeCancelService(TradeRegistry trades, ILogger<TradeCancelService> logger) : ITradeCancelService
{
    public TradeCancelResult Cancel(int askerId)
    {
        if (!trades.TryCancel(askerId, out var targetId))
        {
            logger.LogDebug("Trade cancel ignored: character {AskerId} has no pending ask to cancel", askerId);
            return new TradeCancelResult(false, 0);
        }

        logger.LogDebug("Trade ask cancelled: character {AskerId} withdrew ask to character {TargetId}", askerId,
            targetId);
        return new TradeCancelResult(true, targetId);
    }
}
