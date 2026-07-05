using System.Collections.Immutable;
using Fenrir.Application.Game.Forge;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.ItemModification.Services;

public enum DestroyItemOutcome
{
    Rejected,
    Applied
}

public readonly record struct DestroyItemResult(
    DestroyItemOutcome Outcome,
    int Money,
    int StoneItemId,
    int Quantity,
    int Serial);

public interface IDestroyItemService
{
    ValueTask<DestroyItemResult> DestroyAsync(DestroyItemRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}

/// <summary>
///     Business logic for op89, CZ_DESTROY_ITEM_SEND -- extracted from <see cref="DestroyItemHandler" />, see
///     that handler's remarks.
/// </summary>
public sealed class DestroyItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<DestroyItemService> logger)
    : IDestroyItemService
{
    public async ValueTask<DestroyItemResult> DestroyAsync(DestroyItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1))
            return new DestroyItemResult(DestroyItemOutcome.Rejected, 0, 0, 0, 0);

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (targetStack is not { } target || !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition))
            return new DestroyItemResult(DestroyItemOutcome.Rejected, 0, 0, 0, 0);

        var resolved = DestroyResolver.Resolve(targetDefinition.Item, target);
        if (resolved.Outcome == DestroyResolver.DestroyOutcome.Rejected ||
            !worldData.ItemsById.TryGetValue(resolved.StoneItemId, out var stoneDefinition))
            return new DestroyItemResult(DestroyItemOutcome.Rejected, 0, 0, 0, 0);

        var quantity = stoneDefinition.Item.Sort == 99 ? 1 : 0;
        var newStack = new ItemStack(resolved.StoneItemId, quantity, 0, 0, 0, 0, 0, 0, 0, target.ExpireDate,
            target.Serial);

        var projected = state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, newStack);

        try
        {
            await characters.AdjustMoneyAndReplaceContainerAsync(characterId, resolved.Money, 0, (byte)page1,
                ToTvps(projected), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} destroy-item AdjustMoneyAndReplaceContainerAsync failed (treated as money-cap overflow)",
                characterId);
            return new DestroyItemResult(DestroyItemOutcome.Rejected, 0, 0, 0, 0);
        }

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped destroy-item mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new DestroyItemResult(DestroyItemOutcome.Applied, resolved.Money, resolved.StoneItemId, quantity,
            target.Serial);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
