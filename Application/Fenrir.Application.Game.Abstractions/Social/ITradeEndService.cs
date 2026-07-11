namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct TradeEndResult(bool Handled, int PlayerAId, int PlayerBId,
    int PlayerABigMoneyRestore = 0, int PlayerBBigMoneyRestore = 0);

public interface ITradeEndService
{
    public TradeEndResult End(int characterId);
}
