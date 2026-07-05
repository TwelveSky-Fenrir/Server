using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeCancelService(TradeRegistry trades) : ITradeCancelService
{
    public TradeCancelResult Cancel(int askerId)
    {
        return trades.TryCancel(askerId, out var targetId)
            ? new TradeCancelResult(true, targetId)
            : new TradeCancelResult(false, 0);
    }
}
