using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Progression;

public enum TowerUpgradeOutcome
{
    Aborted,

    Success
}

public readonly record struct TowerUpgradeResult(TowerUpgradeOutcome Outcome, int PackedPage, int PackedIndex);

public interface ITowerUpgradeService
{
    public ValueTask<TowerUpgradeResult> UpgradeAsync(int characterId, Zone zone, PlayerRuntimeState state,
        TowerUpgradeRequest packet, CancellationToken cancellationToken);

    public ValueTask<UseInventoryItemResponse> ConstructAsync(int characterId, Zone zone, PlayerRuntimeState state,
        byte page, byte index, ItemStack item, int constructType, CancellationToken cancellationToken);

    public ValueTask<UseInventoryItemResponse> HealAsync(int characterId, Zone zone, PlayerRuntimeState state,
        byte page, byte index, ItemStack item, CancellationToken cancellationToken);
}
