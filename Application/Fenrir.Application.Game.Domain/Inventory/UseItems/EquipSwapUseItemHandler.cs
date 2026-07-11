using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 double-click-to-equip. Confirms the swap via <see cref="EquipSwapResolver" />, then applies it as
///     one atomic two-container write (the addressed inventory page + the Equipment container) and mirrors the
///     recomputed equipment stats — reusing <see cref="EquipmentService.RecomputeStats" /> exactly as the
///     drag-to-equip path (<c>GenericActionService</c>) does, never re-implementing the stat math. Every
///     rejection (not idle, ineligible item, or a derived slot out of range — each a disconnect in the legacy)
///     collapses to a clean result-1 reply, the same op23 simplification the service's own families use.
/// </summary>
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
            logger.LogDebug(
                "Character {CharacterId} op23 equip-swap rejected: {Outcome} (item {ItemId}, action-sort {ActionSort})",
                context.CharacterId, resolved.Outcome, context.Item.ItemId, state.ActionSort);
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

    /// <summary>Registry routing predicate — see <see cref="EquipSwapResolver.ClaimsItem" />.</summary>
    public static bool ClaimsItem(ItemRowDto item)
    {
        return EquipSwapResolver.ClaimsItem(item);
    }
}
