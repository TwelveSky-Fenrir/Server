using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Result of a menu-lock attempt: whether this call advanced this side's own menu state.</summary>
public readonly record struct TradeLockAttempt(bool Locked, TradeSession? Trade);

public interface ITradeLockService
{
    /// <summary>2-notch confirm: first call locks (menu 0-&gt;1), second confirms (1-&gt;2).</summary>
    public TradeLockAttempt TryLock(int characterId);

    /// <summary>
    ///     Commits atomically (<see cref="ICharacterRepository.ExecuteTradeAsync" />) and mirrors each side's
    ///     new container back to their own zone. An overflow aborts the whole commit -- no partial state.
    /// </summary>
    public ValueTask CommitAsync(TradeSession trade, PlayerRuntimeState playerA, Zone zoneA, PlayerRuntimeState playerB,
        Zone zoneB, int characterId, CancellationToken cancellationToken);
}
