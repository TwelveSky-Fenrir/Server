namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct TradeEndResult(bool Handled, int PlayerAId, int PlayerBId);

public interface ITradeEndService
{
    public TradeEndResult End(int characterId);
}
