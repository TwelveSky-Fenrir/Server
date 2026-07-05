namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Outcome of a CZ_TRADE_CANCEL_SEND attempt.</summary>
public readonly record struct TradeCancelResult(bool Handled, int TargetId);

public interface ITradeCancelService
{
    public TradeCancelResult Cancel(int askerId);
}
