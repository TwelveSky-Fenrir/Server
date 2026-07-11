using Fenrir.Application.Game.Domain.Social.Trade;

namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct TradeStartResult(bool Handled, TradeSession? Trade);

public interface ITradeStartService
{
    public TradeStartResult Start(int callerId);

        public void AbortStart(int callerId);
}
