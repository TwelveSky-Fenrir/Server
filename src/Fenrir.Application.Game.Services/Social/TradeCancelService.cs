using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeCancelService(TradeRegistry trades, ZoneRegistry zones, ILogger<TradeCancelService> logger)
    : ITradeCancelService
{
    public TradeCancelResult Cancel(int askerId)
    {
        if (!trades.TryPeekPending(askerId, out var targetId, out var isAsker) || !isAsker)
        {
            logger.LogDebug("Trade cancel ignored: character {AskerId} has no pending ask to cancel", askerId);
            return new TradeCancelResult(false, 0);
        }

        var transition = trades.TryEnterTransition(askerId, targetId);
        if (transition is null)
        {
            logger.LogDebug(
                "Trade cancel ignored: character {AskerId} / target {TargetId} already have an in-flight transition",
                askerId, targetId);
            return new TradeCancelResult(false, 0);
        }

        using (transition)
        {
            if (!trades.TryPeekPending(askerId, out targetId, out isAsker) || !isAsker)
            {
                logger.LogDebug("Trade cancel ignored: character {AskerId} has no pending ask to cancel", askerId);
                return new TradeCancelResult(false, 0);
            }

            if (!zones.TryGetPlayer(targetId, out var target) || target.IsMovingZone)
            {
                logger.LogDebug(
                    "Trade cancel rejected: character {AskerId}'s target {TargetId} is unreachable or mid zone-transfer -- pending pair left unchanged",
                    askerId, targetId);
                return new TradeCancelResult(false, 0);
            }

            if (!trades.TryCancel(askerId, out var cancelledTargetId) || cancelledTargetId != targetId)
            {
                logger.LogDebug("Trade cancel ignored: character {AskerId} has no matching pending ask", askerId);
                return new TradeCancelResult(false, 0);
            }

            logger.LogDebug("Trade ask cancelled: character {AskerId} withdrew ask to character {TargetId}", askerId,
                targetId);
            return new TradeCancelResult(true, targetId);
        }
    }
}
