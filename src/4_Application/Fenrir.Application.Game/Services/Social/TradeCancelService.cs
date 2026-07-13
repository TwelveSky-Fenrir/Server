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
        if (!trades.TryCancel(askerId, out var targetId))
        {
            logger.LogDebug("Trade cancel ignored: character {AskerId} has no pending ask to cancel", askerId);
            return new TradeCancelResult(false, 0);
        }

        if (!zones.TryGetPlayer(targetId, out var target) || target.IsMovingZone)
        {
            logger.LogDebug(
                "Trade cancel: character {AskerId} withdrew its own pending ask, but target {TargetId} is unreachable or mid zone-transfer -- no notice sent, target's own record left as-is",
                askerId, targetId);
            return new TradeCancelResult(false, 0);
        }

        trades.ClearTargetAfterCancel(targetId, askerId);

        logger.LogDebug("Trade ask cancelled: character {AskerId} withdrew ask to character {TargetId}", askerId,
            targetId);
        return new TradeCancelResult(true, targetId);
    }
}
