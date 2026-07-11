using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class DestroyItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<DestroyItemService> logger)
    : IDestroyItemService
{

        private const short DestroyItemEventCode = 1;

        private const byte SuccessOutcome = 1;

    public async ValueTask<DestroyItemResult> DestroyAsync(DestroyItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1))
        {
            logger.LogDebug("Character {CharacterId} destroy-item rejected: invalid slot ({Page1}:{Index1})",
                characterId, page1, index1);
            return new DestroyItemResult(DestroyItemOutcome.Rejected, 0, 0, 0, 0);
        }

        if (page1 == ContainerMatrix.InventoryPage1 && state.InventoryDate < GameDate.Today())
        {
            logger.LogDebug(
                "Character {CharacterId} destroy-item rejected: dated-vault last page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return new DestroyItemResult(DestroyItemOutcome.Rejected, 0, 0, 0, 0);
        }

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (targetStack is not { } target || !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition))
        {
            logger.LogDebug("Character {CharacterId} destroy-item rejected: target slot empty/unresolvable",
                characterId);
            return new DestroyItemResult(DestroyItemOutcome.Rejected, 0, 0, 0, 0);
        }

        var resolved = DestroyResolver.Resolve(targetDefinition.Item, target);
        if (resolved.Outcome == DestroyResolver.DestroyOutcome.Rejected ||
            !worldData.ItemsById.TryGetValue(resolved.StoneItemId, out var stoneDefinition))
        {
            logger.LogInformation(
                "Character {CharacterId} destroy-item rejected by resolver (target {TargetItemId})", characterId,
                target.ItemId);
            return new DestroyItemResult(DestroyItemOutcome.Rejected, 0, 0, 0, 0);
        }

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

        await eventLog.LogAsync(DestroyItemEventCode, EventLogCategory.ItemDestroy, accountId, characterId,
            null, null, null, resolved.Money, null, target.ItemId, target.Quantity, SuccessOutcome,
            $"StoneItemId={resolved.StoneItemId};StoneQuantity={quantity};Enchant={target.Enchant};Serial={target.Serial}",
            cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped destroy-item mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} destroy-item applied: target {TargetItemId} dissolved into {Money} money + {StoneItemId} x{Quantity}",
            characterId, target.ItemId, resolved.Money, resolved.StoneItemId, quantity);

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
