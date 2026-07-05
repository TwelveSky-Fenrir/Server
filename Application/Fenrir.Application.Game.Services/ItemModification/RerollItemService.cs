using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for op26, CZ_EXCHANGE_ITEM_SEND -- extracted from <see cref="RerollItemHandler" />, see
///     that handler's remarks.
/// </summary>
public sealed class RerollItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<RerollItemService> logger)
    : IRerollItemService
{
    public async ValueTask<RerollItemResult> RerollAsync(RerollItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1))
            return new RerollItemResult(RerollItemOutcome.Rejected, 0, [0, 0, 0, 0, 0, 0]);

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (targetStack is not { } target || !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition))
            return new RerollItemResult(RerollItemOutcome.Rejected, 0, [0, 0, 0, 0, 0, 0]);

        var catalog = worldData.ItemsById.Values.Select(d => d.Item);
        var resolved = RerollResolver.Resolve(targetDefinition.Item, state.PreviousTribe, catalog,
            SystemRandomSource.Instance);

        if (resolved.Outcome == RerollResolver.RerollOutcome.Rejected)
            return new RerollItemResult(RerollItemOutcome.Rejected, 0, [0, 0, 0, 0, 0, 0]);

        if (resolved.Outcome == RerollResolver.RerollOutcome.NoCandidate)
            return new RerollItemResult(RerollItemOutcome.NoCandidate, resolved.Cost, [0, 0, 0, 0, 0, 0]);

        var newStack = target with { ItemId = resolved.ResultItemId };
        var projected = state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, newStack);

        try
        {
            await characters.AdjustMoneyAndReplaceContainerAsync(characterId, -resolved.Cost, 0, (byte)page1,
                ToTvps(projected), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} reroll-item AdjustMoneyAndReplaceContainerAsync failed (treated as insufficient funds)",
                characterId);
            return new RerollItemResult(RerollItemOutcome.Rejected, 0, [0, 0, 0, 0, 0, 0]);
        }

        var packedValue = ItemValueCodec.Encode(target.Enchant, target.Combine, target.Refine, target.Socket);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped reroll mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new RerollItemResult(RerollItemOutcome.Applied, resolved.Cost,
            [resolved.ResultItemId, 0, 0, target.Quantity, packedValue, target.Serial]);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
