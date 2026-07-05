using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>On accept, both sides may send CZ_TRADE_START_SEND (symmetric).</summary>
public sealed class TradeAnswerService(TradeRegistry trades) : ITradeAnswerService
{
    public TradeAnswerResult Answer(int targetId, int answer)
    {
        if (answer is not (0 or 1 or 2))
            return new TradeAnswerResult(false, 0);

        if (!trades.TryAnswer(targetId, answer == 0, out var askerId))
            return new TradeAnswerResult(false, 0);

        return new TradeAnswerResult(true, askerId);
    }
}
