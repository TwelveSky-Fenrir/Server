using Fenrir.Application.Game.Social.Trade;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Result of a CZ_TRADE_START_SEND attempt.</summary>
public readonly record struct TradeStartResult(bool Handled, TradeSession? Trade);

public interface ITradeStartService
{
    TradeStartResult Start(int callerId);
}

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
