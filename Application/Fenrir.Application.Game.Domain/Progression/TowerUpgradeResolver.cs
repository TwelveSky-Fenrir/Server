using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Progression;

public static class TowerUpgradeResolver
{
    public enum Outcome
    {
        Disconnect,
        Success
    }

    public const int TowerCount = TowerWarState.TowerCount;

        public static Result Validate(byte tribeRole, int requestZoneNumber, int actualZoneNumber, byte tribe,
        int requestedType, int requestedNextLevelRaw, int currentPackedState, bool towerValid)
    {
        if (tribeRole is not (1 or 2) || !towerValid || requestZoneNumber != actualZoneNumber)
            return Reject();

        var towerIndex = TowerZoneIndexTable.GetTowerIndex(actualZoneNumber);
        if (towerIndex is < 0 or >= TowerCount)
            return Reject();

        var tribeBlockStart = tribe * 3;
        if (towerIndex < tribeBlockStart || towerIndex > tribeBlockStart + 3 || requestedType is < 1 or > 3)
            return Reject();

        var requestedNextLevel = requestedNextLevelRaw switch
        {
            2 => 4,
            4 => 6,
            6 => 8,
            _ => -1
        };
        if (requestedNextLevel < 0)
            return Reject();

        var currentLevel = TowerWarState.DecodeLevel(currentPackedState);
        if (currentLevel is not (2 or 4 or 6))
            return Reject();

        var currentType = TowerWarState.DecodeType(currentPackedState);
        if (currentType != requestedType)
            return Reject();

        var currentNextLevel = currentLevel switch
        {
            2 => 4,
            4 => 6,
            6 => 8,
            _ => 0
        };
        return currentNextLevel != requestedNextLevel
            ? Reject()
            : new Result(Outcome.Success, towerIndex, requestedNextLevel * 100 + requestedType);
    }

        public static bool TryFindMaterial(ImmutableDictionary<byte, ItemStack> page0,
        ImmutableDictionary<byte, ItemStack> page1, int itemId, out byte page, out byte slot)
    {
        for (var i = 0; i <= 63; i++)
            if (page0.TryGetValue((byte)i, out var stack) && stack.ItemId == itemId)
            {
                page = ContainerMatrix.InventoryPage0;
                slot = (byte)i;
                return true;
            }

        for (var i = 0; i <= 63; i++)
            if (page1.TryGetValue((byte)i, out var stack) && stack.ItemId == itemId)
            {
                page = ContainerMatrix.InventoryPage1;
                slot = (byte)i;
                return true;
            }

        page = 0;
        slot = 0;
        return false;
    }

    private static Result Reject()
    {
        return new Result(Outcome.Disconnect, -1, 0);
    }

    public readonly record struct Result(Outcome Outcome, int TowerIndex, int NewPackedState);
}
