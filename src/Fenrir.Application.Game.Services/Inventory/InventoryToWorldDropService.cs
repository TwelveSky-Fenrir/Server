using System.Collections.Immutable;
using CaeriusNet.Exceptions;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Inventory;

public sealed class InventoryToWorldDropService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    PartyRegistry partyRegistry,
    ILogger<InventoryToWorldDropService> logger)
    : IInventoryToWorldDropService
{
    private const short ManualDropItemEventCode = 1;

    private const byte SuccessOutcome = 1;

    private const byte NonDroppableFlagValue = 1;

    public async ValueTask<InventoryToWorldDropResult> DropToWorldAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, DefaultPData move, bool premiumPageAccessAllowed,
        CancellationToken cancellationToken)
    {
        var sourcePage = move.Page1;
        var sourceSlot = move.Index1;

        var source = sourcePage is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
                     ContainerMatrix.IsValidSlot((byte)sourcePage, sourceSlot)
            ? state.Inventory.GetSlot((byte)sourcePage, (byte)sourceSlot)
            : null;

        var itemDefinition = source is { } stack && worldData.ItemsById.TryGetValue(stack.ItemId, out var definition)
            ? definition
            : null;

        var hasLegacySingleQuantity = source is { } sourceItem && itemDefinition is { } resolvedDefinition &&
                                      resolvedDefinition.Item.Sort switch
                                      {
                                          ItemQuantityPolicy.PetSort => sourceItem.Quantity is >=
                                              ItemQuantityPolicy.MinStackQuantity and <=
                                              ItemQuantityPolicy.MaxPetActivity,
                                          _ when ItemQuantityPolicy.CarriesNoQuantity(resolvedDefinition.Item.Sort) =>
                                              sourceItem.Quantity is 0 or 1,
                                          _ => false
                                      };
        var requestedQuantity = move.Quantity1 == 0 && hasLegacySingleQuantity
            ? 1
            : move.Quantity1;

        var partyName = PartyIdentityResolver.ResolveCurrentPartyName(partyRegistry, characterId, state.Name,
            memberId => zone.TryGetPlayer(memberId, out var member) ? member?.Name : null);

        var resolved = InventoryToWorldDropPolicy.Resolve(
            sourcePage, sourceSlot, requestedQuantity, premiumPageAccessAllowed,
            source, itemDefinition,
            itemDefinition is null || itemDefinition.Item.CheckAvatarDrop != NonDroppableFlagValue,
            static _ => GroundItemSpawnEligibility.Eligible,
            zone.GroundItemCount,
            state.PosX, state.PosY, state.PosZ,
            state.Name, partyName);

        if (resolved.IsMalformed)
            return InventoryToWorldDropResult.Aborted;

        if (resolved.IsSoftFailure)
            return InventoryToWorldDropResult.Failed;

        var container = (byte)sourcePage;
        var slot = (byte)sourceSlot;
        var original = state.Inventory.GetContainer(container);
        var projected = resolved.NewSource is { } newStack
            ? state.Inventory.GetContainer(container).SetItem(slot, newStack)
            : state.Inventory.GetContainer(container).Remove(slot);

        try
        {
            await characters.ReplaceContainerAsync(characterId, container, ToTvps(projected), cancellationToken);
        }
        catch (CaeriusNetSqlException exception)
        {
            logger.LogError(exception,
                "Character {CharacterId} inventory-to-world drop could not persist its source container; no zone mutation was applied",
                characterId);
            return InventoryToWorldDropResult.Failed;
        }

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projected));

        if ((await zone.PostInventoryAndGroundItemCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, containers, null, GroundItemSpawn: resolved.Spawn),
                cancellationToken)).Kind != ZoneCommandResultKind.Applied)
        {
            await characters.ReplaceContainerAsync(characterId, container, ToTvps(original), cancellationToken);
            logger.LogWarning(
                "Zone {MapId} rejected inventory-to-world drop for character {CharacterId}; restored the persisted inventory container",
                zone.MapId, characterId);
            return InventoryToWorldDropResult.Failed;
        }

        if (source is { } droppedStack && !ContainerMatrix.IsStackableSort(itemDefinition!.Item.Sort))
            await eventLog.LogAsync(ManualDropItemEventCode, EventLogCategory.ItemDrop, accountId, characterId,
                null, null, null, null, null, droppedStack.ItemId, droppedStack.Quantity, SuccessOutcome,
                $"Serial={droppedStack.Serial};Enchant={droppedStack.Enchant};Combine={droppedStack.Combine};" +
                $"Refine={droppedStack.Refine};Socket={droppedStack.Socket}",
                cancellationToken);

        return new InventoryToWorldDropResult(InventoryToWorldDropStatus.Succeeded, resolved.Spawn);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
