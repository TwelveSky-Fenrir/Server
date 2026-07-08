using Fenrir.Application.Game.Domain.Social.Trade;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Result of a CZ_TRADE_START_SEND attempt.</summary>
public readonly record struct TradeStartResult(bool Handled, TradeSession? Trade);

public interface ITradeStartService
{
    public TradeStartResult Start(int callerId);

    /// <summary>
    ///     Rolls a just-started session back to idle for <paramref name="callerId" /> only, after the
    ///     caller's own post-commit re-validation of the trade partner (zone lookup) failed. The partner's
    ///     own trade-process state, if any, is deliberately left untouched.
    /// </summary>
    public void AbortStart(int callerId);
}
