using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>Callable by either accepted side; ZC_TRADE_START_RECV is crossed (each player receives the OTHER's offer).</summary>
public sealed class TradeStartService(TradeRegistry trades) : ITradeStartService
{
    public TradeStartResult Start(int callerId)
    {
        return trades.TryStart(callerId, out var trade)
            ? new TradeStartResult(true, trade)
            : new TradeStartResult(false, null);
    }
}
