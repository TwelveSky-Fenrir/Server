namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Outcome of a CZ_TRADE_ANSWER_SEND attempt.</summary>
public readonly record struct TradeAnswerResult(bool Handled, int AskerId);

public interface ITradeAnswerService
{
    public TradeAnswerResult Answer(int targetId, int answer);
}
