using Fenrir.Application.Game.GameData;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class EquipSwapUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    ILogger<EquipSwapUseItemHandler> logger) : IUseItemHandler
{
    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var equipRow = context.Definition.Item;
        var candidate = new EquipItemValidationGate.EquipCandidate(equipRow.ItemId, equipRow.EquipInfo1,
            equipRow.EquipInfo2, equipRow.LevelLimit, equipRow.MartialLevelLimit, equipRow.CheckSetItem,
            equipRow.Sort);

        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);

        var resolved = EquipSwapResolver.Resolve(context.Item, candidate, equipmentContainer, state.ActionSort,
            state.PreviousTribe, state.CombinedLevel, state.RebirthCount);

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} op23 equip-swap rejected: {Outcome} (item {ItemId}, action-sort {ActionSort}) -- disconnecting (legacy Quit(), no acknowledgment)",
                context.CharacterId, resolved.Outcome, context.Item.ItemId, state.ActionSort);
            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.StateViolation);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var inventoryPage = state.Inventory.GetContainer(context.Page);
        var newInventory = resolved.NewInventoryStack is { } previouslyEquipped
            ? inventoryPage.SetItem(context.Index, previouslyEquipped)
            : inventoryPage.Remove(context.Index);
        var newEquipment = equipmentContainer.SetItem(resolved.TargetEquipSlot, resolved.NewEquipStack);

        var updatedStats = UseItemStatRecompute.WithEquipment(state, worldData, newEquipment);

        await inventoryWriter.ReplaceTwoAndMirrorAsync(context.Zone, context.CharacterId, context.Page, newInventory,
            ContainerMatrix.Equipment, newEquipment, updatedStats, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} op23 double-click-to-equip: item {ItemId} swapped into equip slot {Slot}",
            context.CharacterId, context.Item.ItemId, resolved.TargetEquipSlot);

        return UseItemResponses.Success(context.Page, context.Index);
    }

    public static bool ClaimsItem(ItemRowDto item)
    {
        return EquipSwapResolver.ClaimsItem(item);
    }
}
