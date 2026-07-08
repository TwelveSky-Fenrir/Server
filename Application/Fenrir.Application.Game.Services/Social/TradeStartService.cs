using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>Callable by either accepted side; ZC_TRADE_START_RECV is crossed (each player receives the OTHER's offer).</summary>
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

    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8600-8639 (TradeStart handler, full body) ;
    ///     S04_MyWork02.cpp:8607-8628 (the post-commit partner re-validation failure path specifically --
    ///     unlike TradeCancel/TradeAnswer/TradeEnd, which leave an already-applied caller-side state change
    ///     standing on a failed partner lookup, TradeStart's failure path resets the caller's own
    ///     trade-process state back to 0 while leaving the partner's side untouched).
    /// </remarks>
    public void AbortStart(int callerId)
    {
        if (trades.TryAbortStartForCaller(callerId))
            logger.LogDebug(
                "Trade start rolled back: character {CallerId}'s post-commit partner re-validation failed; " +
                "caller reset to idle, partner state left untouched", callerId);
    }
}
