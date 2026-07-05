using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Enchant;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Npcs;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.ItemModification.Services;

public enum EnchantItemOutcome
{
    Rejected,
    NotSupported,
    Applied
}

public readonly record struct EnchantItemResult(EnchantItemOutcome Outcome, int ResultCode, int Cost, int NewEnchant);

public interface IEnchantItemService
{
    ValueTask<EnchantItemResult> EnchantAsync(EnchantItemRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}

/// <summary>
///     Business logic for op24, CZ_IMPROVE_ITEM_SEND -- extracted from <see cref="EnchantItemHandler" />, see
///     that handler's remarks.
/// </summary>
public sealed class EnchantItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<EnchantItemService> logger)
    : IEnchantItemService
{
    public async ValueTask<EnchantItemResult> EnchantAsync(EnchantItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId))
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);

        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1) ||
            page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page2, index2))
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var materialStack = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (targetStack is not { } target || materialStack is not { } material ||
            !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition) ||
            !worldData.ItemsById.TryGetValue(material.ItemId, out var materialDefinition))
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);

        var luck = state.Stats?.Luck ?? 0;

        var resolved = EnchantResolver.Resolve(targetDefinition, target, materialDefinition, luck,
            0, SystemRandomSource.Instance);

        if (resolved.Outcome == EnchantResolver.EnchantOutcome.NotSupported)
            return new EnchantItemResult(EnchantItemOutcome.NotSupported, 0, 0, 0);

        if (resolved.Outcome == EnchantResolver.EnchantOutcome.Rejected)
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);

        // Material is always consumed exactly once regardless of outcome.
        var remainingMaterialQuantity = material.Quantity - 1;
        var newMaterialStack = remainingMaterialQuantity > 0
            ? material with { Quantity = remainingMaterialQuantity }
            : (ItemStack?)null;

        ItemStack? newTargetStack = resolved.Outcome == EnchantResolver.EnchantOutcome.Destroyed
            ? null
            : target with { Enchant = (byte)resolved.NewEnchant };

        ImmutableDictionary<byte, ItemStack> projectedTargetContainer;
        ImmutableDictionary<byte, ItemStack> projectedMaterialContainer;

        if (page1 == page2)
        {
            var combined = ApplySlotChange(state.Inventory.GetContainer((byte)page1), (byte)index1, newTargetStack);
            combined = ApplySlotChange(combined, (byte)index2, newMaterialStack);
            projectedTargetContainer = combined;
            projectedMaterialContainer = combined;
        }
        else
        {
            projectedTargetContainer =
                ApplySlotChange(state.Inventory.GetContainer((byte)page1), (byte)index1, newTargetStack);
            projectedMaterialContainer =
                ApplySlotChange(state.Inventory.GetContainer((byte)page2), (byte)index2, newMaterialStack);
        }

        try
        {
            if (page1 == page2)
                await characters.AdjustMoneyAndReplaceContainerAsync(characterId, -resolved.Cost, 0, (byte)page1,
                    ToTvps(projectedTargetContainer), cancellationToken);
            else
                await characters.AdjustMoneyAndReplaceTwoContainersAsync(characterId, -resolved.Cost, 0,
                    (byte)page1, ToTvps(projectedTargetContainer), (byte)page2, ToTvps(projectedMaterialContainer),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} enchant AdjustMoney...ReplaceContainer(s)Async failed (treated as insufficient funds)",
                characterId);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projectedTargetContainer),
                new InventoryContainerSnapshot((byte)page2, projectedMaterialContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped enchant mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new EnchantItemResult(EnchantItemOutcome.Applied, MapResultCode(resolved.Outcome), resolved.Cost,
            resolved.NewEnchant);
    }

    /// <summary>ZC_IMPROVE_ITEM_RECV codes: 0 success, 1 fail, 2 destroyed, 3 reset-to-+40, 4 protected.</summary>
    private static int MapResultCode(EnchantResolver.EnchantOutcome outcome)
    {
        return outcome switch
        {
            EnchantResolver.EnchantOutcome.Unsealed => 0,
            EnchantResolver.EnchantOutcome.Success => 0,
            EnchantResolver.EnchantOutcome.Failed => 1,
            EnchantResolver.EnchantOutcome.Destroyed => 2,
            EnchantResolver.EnchantOutcome.ResetToForty => 3,
            EnchantResolver.EnchantOutcome.Protected => 4,
            _ => 1
        };
    }

    private static ImmutableDictionary<byte, ItemStack> ApplySlotChange(
        ImmutableDictionary<byte, ItemStack> current, byte slot, ItemStack? value)
    {
        return value is { } v ? current.SetItem(slot, v) : current.Remove(slot);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
