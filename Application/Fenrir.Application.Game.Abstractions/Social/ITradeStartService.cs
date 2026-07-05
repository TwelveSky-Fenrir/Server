using Fenrir.Application.Game.Domain.Social.Trade;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Result of a CZ_TRADE_START_SEND attempt.</summary>
public readonly record struct TradeStartResult(bool Handled, TradeSession? Trade);

public interface ITradeStartService
{
    public TradeStartResult Start(int callerId);
}
