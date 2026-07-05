namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Result of a CZ_TRADE_END_SEND attempt.</summary>
public readonly record struct TradeEndResult(bool Handled, int PlayerAId, int PlayerBId);

public interface ITradeEndService
{
    public TradeEndResult End(int characterId);
}
