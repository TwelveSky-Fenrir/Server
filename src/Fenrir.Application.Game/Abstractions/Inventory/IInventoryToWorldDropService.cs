using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Inventory;

public enum InventoryToWorldDropStatus
{
    Aborted,
    Failed,
    Succeeded
}

public readonly record struct InventoryToWorldDropResult(
    InventoryToWorldDropStatus Status,
    GroundItemSpawnPlan? Spawn = null)
{
    public static readonly InventoryToWorldDropResult Aborted = new(InventoryToWorldDropStatus.Aborted);
    public static readonly InventoryToWorldDropResult Failed = new(InventoryToWorldDropStatus.Failed);
}

public interface IInventoryToWorldDropService
{
    public ValueTask<InventoryToWorldDropResult> DropToWorldAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, DefaultPData move, bool premiumPageAccessAllowed,
        CancellationToken cancellationToken);
}
