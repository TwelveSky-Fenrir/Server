using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>On accept, both sides may send CZ_TRADE_START_SEND (symmetric).</summary>
public sealed class TradeAnswerService(TradeRegistry trades, ILogger<TradeAnswerService> logger) : ITradeAnswerService
{
    public TradeAnswerResult Answer(int targetId, int answer)
    {
        if (answer is not (0 or 1 or 2))
        {
            logger.LogDebug("Trade answer rejected: character {TargetId} sent malformed answer code {Answer}",
                targetId, answer);
            return new TradeAnswerResult(false, 0);
        }

        if (!trades.TryAnswer(targetId, answer == 0, out var askerId))
        {
            logger.LogDebug("Trade answer ignored: character {TargetId} has no pending ask", targetId);
            return new TradeAnswerResult(false, 0);
        }

        logger.LogDebug("Trade ask {Outcome}: character {TargetId} answered asker {AskerId}",
            answer == 0 ? "accepted" : "declined", targetId, askerId);
        return new TradeAnswerResult(true, askerId);
    }
}
