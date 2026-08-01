using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct TradeLockAttempt(bool Locked, TradeSession? Trade);

public interface ITradeLockService
{
    public TradeLockAttempt TryLock(int characterId);

    public ValueTask CommitAsync(TradeSession trade, PlayerRuntimeState playerA, Zone zoneA, PlayerRuntimeState playerB,
        Zone zoneB, int characterId, CancellationToken cancellationToken);
}
