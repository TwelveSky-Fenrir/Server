namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct TradeCancelResult(bool Handled, int TargetId);

public interface ITradeCancelService
{
    public TradeCancelResult Cancel(int askerId);
}
