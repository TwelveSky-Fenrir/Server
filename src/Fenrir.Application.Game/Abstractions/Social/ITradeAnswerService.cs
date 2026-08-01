namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct TradeAnswerResult(bool Handled, int AskerId);

public interface ITradeAnswerService
{
    public TradeAnswerResult Answer(int targetId, int answer);
}
