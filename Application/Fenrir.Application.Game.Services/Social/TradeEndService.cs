using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;

namespace Fenrir.Application.Game.Services.Social;

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
