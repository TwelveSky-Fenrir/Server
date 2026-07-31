using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class InventoryEquipDragDropTransferGate
{
    public enum Outcome
    {
        Valid,
        IndexOutOfRange,
        DatedStoragePageExpired
    }

    private const int DropPositionMin = 0;
    private const int DropPositionMax = 7;

    public static Outcome Evaluate(int sort, DefaultPData move, int inventoryDate)
    {
        return sort switch
        {
            210 => EvaluateInventoryToEquip(move, inventoryDate),
            213 => EvaluateEquipToInventory(move, inventoryDate),
            _ => Outcome.Valid
        };
    }

    private static Outcome EvaluateInventoryToEquip(DefaultPData move, int inventoryDate)
    {
        var sourcePageInRange = move.Page1 is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1;
        if (!sourcePageInRange ||
            !ContainerMatrix.IsValidSlot((byte)move.Page1, move.Index1) ||
            !ContainerMatrix.IsValidSlot(ContainerMatrix.Equipment, move.Index2))
            return Outcome.IndexOutOfRange;

        return IsExpiredDatedStoragePage(move.Page1, inventoryDate) ? Outcome.DatedStoragePageExpired : Outcome.Valid;
    }

    private static Outcome EvaluateEquipToInventory(DefaultPData move, int inventoryDate)
    {
        var destinationPageInRange = move.Page2 is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1;
        if (!ContainerMatrix.IsValidSlot(ContainerMatrix.Equipment, move.Index1) ||
            !destinationPageInRange ||
            !ContainerMatrix.IsValidSlot((byte)move.Page2, move.Index2) ||
            move.XPost2 is < DropPositionMin or > DropPositionMax ||
            move.YPost2 is < DropPositionMin or > DropPositionMax)
            return Outcome.IndexOutOfRange;

        return IsExpiredDatedStoragePage(move.Page2, inventoryDate) ? Outcome.DatedStoragePageExpired : Outcome.Valid;
    }

    private static bool IsExpiredDatedStoragePage(int page, int inventoryDate)
    {
        return page == ContainerMatrix.InventoryPage1 && inventoryDate < GameDate.Today();
    }
}
