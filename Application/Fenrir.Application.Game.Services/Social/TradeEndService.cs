using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeEndService(TradeRegistry trades, ILogger<TradeEndService> logger) : ITradeEndService
{
    public TradeEndResult End(int characterId)
    {
        if (!trades.TryEnd(characterId, out var trade) || trade is null)
        {
            logger.LogDebug("Trade end ignored: character {CharacterId} has no active trade session", characterId);
            return new TradeEndResult(false, 0, 0);
        }

        logger.LogDebug(
            "Trade abandoned (nothing committed): character {CharacterId} ended session with character {PlayerAId}/{PlayerBId}",
            characterId, trade.PlayerAId, trade.PlayerBId);
        return new TradeEndResult(true, trade.PlayerAId, trade.PlayerBId, trade.SideA.BigMoney,
            trade.SideB.BigMoney);
    }
}
