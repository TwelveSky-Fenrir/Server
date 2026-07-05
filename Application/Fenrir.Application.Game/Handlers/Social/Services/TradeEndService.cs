using Fenrir.Application.Game.Social.Trade;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Result of a CZ_TRADE_END_SEND attempt.</summary>
public readonly record struct TradeEndResult(bool Handled, int PlayerAId, int PlayerBId);

public interface ITradeEndService
{
    TradeEndResult End(int characterId);
}

/// <summary>Abandons an in-progress trade; nothing was ever committed, so no rollback is needed.</summary>
public sealed class TradeEndService(TradeRegistry trades) : ITradeEndService
{
    public TradeEndResult End(int characterId)
    {
        if (!trades.TryEnd(characterId, out var trade) || trade is null)
            return new TradeEndResult(false, 0, 0);

        return new TradeEndResult(true, trade.PlayerAId, trade.PlayerBId);
    }
}
