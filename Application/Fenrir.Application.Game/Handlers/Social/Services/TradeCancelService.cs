using Fenrir.Application.Game.Social.Trade;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Outcome of a CZ_TRADE_CANCEL_SEND attempt.</summary>
public readonly record struct TradeCancelResult(bool Handled, int TargetId);

public interface ITradeCancelService
{
    TradeCancelResult Cancel(int askerId);
}

public sealed class TradeCancelService(TradeRegistry trades) : ITradeCancelService
{
    public TradeCancelResult Cancel(int askerId)
    {
        return trades.TryCancel(askerId, out var targetId)
            ? new TradeCancelResult(true, targetId)
            : new TradeCancelResult(false, 0);
    }
}
