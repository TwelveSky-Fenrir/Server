using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Inventory;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Inventory;

/// <inheritdoc cref="IPetBagActionService" />
public sealed class PetBagActionService(
    IPetBagRepository petBag,
    WorldDataCache worldData,
    ILogger<PetBagActionService> logger)
    : IPetBagActionService
{
    /// <summary>Sentinel container/slot byte forced when a raw wire value falls outside the safe 0-1/0-19 range.</summary>
    private const byte InvalidByte = 0xFF;

    public async ValueTask<GenericActionResult> DepositAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, bool petBagUpperHalfEntitlementActive, bool secondInventoryPageEntitlementActive,
        CancellationToken cancellationToken)
    {
        // Case 254 field re-map: Page1/Index1 = source inventory page/slot, Page2 = destination pet-bag slot.
        var sourcePage = move.Page1;
        var sourceIndex = move.Index1;
        var petBagSlot = move.Page2;

        var sourceContainer = SafeInventoryPage(sourcePage);
        var source = sourceContainer != InvalidByte && ContainerMatrix.IsValidSlot(sourceContainer, sourceIndex)
            ? state.Inventory.GetSlot(sourceContainer, (byte)sourceIndex)
            : null;

        byte sourceItemSort = 0;
        if (source is { } src && worldData.ItemsById.TryGetValue(src.ItemId, out var itemDefinition))
            sourceItemSort = itemDefinition.Item.Sort;

        var destinationBagItemId = PetBagItemTransferPolicy.IsValidBagSlot(petBagSlot)
            ? state.GetPetBagSlot((byte)petBagSlot)
            : null;

        var petEquipped = IsPetEquipped(state);

        var resolved = PetBagItemTransferPolicy.ResolveDepositFromInventory(sourceContainer, sourceIndex,
            petBagSlot, source, destinationBagItemId, sourceItemSort, petEquipped,
            petBagUpperHalfEntitlementActive, secondInventoryPageEntitlementActive);

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} pet-bag-deposit disconnect: policy rejected ({Outcome})", characterId,
                resolved.Outcome);
            return GenericActionResult.Aborted;
        }

        var projectedContainer = ApplySlotChange(state.Inventory.GetContainer(sourceContainer), (byte)sourceIndex,
            resolved.NewGeneralInventorySlot);

        await petBag.DepositAsync(characterId, sourceContainer, ToTvps(projectedContainer), (byte)petBagSlot,
            resolved.NewPetBagItemId, cancellationToken);

        var command = new PetBagZoneCommand(characterId,
            ImmutableArray.Create(new PetBagSlotWrite((byte)petBagSlot, resolved.NewPetBagItemId)),
            new InventoryContainerSnapshot(sourceContainer, projectedContainer));

        if (!await zone.PostPetBagCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} pet-bag inbox full: dropped deposit mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} pet-bag-deposit applied: inventory {SourceContainer}:{SourceIndex} -> bag slot {PetBagSlot}",
            characterId, sourceContainer, sourceIndex, petBagSlot);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> WithdrawAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, bool petBagUpperHalfEntitlementActive, bool secondInventoryPageEntitlementActive,
        CancellationToken cancellationToken)
    {
        // Case 255 field re-map: Index1 = source pet-bag slot, Quantity1 = destination inventory page, Page2 =
        // destination inventory slot, Index2 = destination cell X, XPost2 = destination cell Y.
        var sourceBagSlot = move.Index1;
        var destinationPage = move.Quantity1;
        var destinationIndex = move.Page2;
        var destinationX = move.Index2;
        var destinationY = move.XPost2;

        var sourceBagItemId = PetBagItemTransferPolicy.IsValidBagSlot(sourceBagSlot)
            ? state.GetPetBagSlot((byte)sourceBagSlot)
            : null;

        var destinationContainer = SafeInventoryPage(destinationPage);
        var destination = destinationContainer != InvalidByte &&
                          ContainerMatrix.IsValidSlot(destinationContainer, destinationIndex)
            ? state.Inventory.GetSlot(destinationContainer, (byte)destinationIndex)
            : null;

        var petEquipped = IsPetEquipped(state);

        // No confirmed non-zero rarity-tier serial formula is modeled here yet -- see
        // PetBagItemTransferPolicy.ResolveWithdrawToInventory's own remarks; 0 is correct for the genuine
        // pet-category items this direction actually handles.
        var resolved = PetBagItemTransferPolicy.ResolveWithdrawToInventory(sourceBagSlot, sourceBagItemId,
            destinationContainer, destinationIndex, destinationX, destinationY, destination, 0, petEquipped,
            petBagUpperHalfEntitlementActive, secondInventoryPageEntitlementActive);

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} pet-bag-withdraw disconnect: policy rejected ({Outcome})", characterId,
                resolved.Outcome);
            return GenericActionResult.Aborted;
        }

        var projectedContainer = ApplySlotChange(state.Inventory.GetContainer(destinationContainer),
            (byte)destinationIndex, resolved.NewGeneralInventorySlot);

        await petBag.WithdrawAsync(characterId, (byte)sourceBagSlot, destinationContainer,
            ToTvps(projectedContainer), cancellationToken);

        var command = new PetBagZoneCommand(characterId,
            ImmutableArray.Create(new PetBagSlotWrite((byte)sourceBagSlot, resolved.NewSourcePetBagItemId)),
            new InventoryContainerSnapshot(destinationContainer, projectedContainer));

        if (!await zone.PostPetBagCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} pet-bag inbox full: dropped withdraw mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} pet-bag-withdraw applied: bag slot {SourceBagSlot} -> inventory {DestContainer}:{DestIndex}",
            characterId, sourceBagSlot, destinationContainer, destinationIndex);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> RearrangeAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, bool petBagUpperHalfEntitlementActive, CancellationToken cancellationToken)
    {
        // Case 256 field re-map: Index1 = source pet-bag slot, Page2 = destination pet-bag slot.
        var sourceBagSlot = move.Index1;
        var destinationBagSlot = move.Page2;

        // Source-equals-destination: PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag already returns a
        // no-mutation Success for this case, bypassing every other guard (pet-equipped, entitlement, catalog
        // lookups) per the C8 contract's own Edge cases section -- short-circuited here BEFORE any lookup so a
        // genuinely bare request never touches the database or the zone tick.
        if (sourceBagSlot == destinationBagSlot && PetBagItemTransferPolicy.IsValidBagSlot(sourceBagSlot))
        {
            logger.LogDebug(
                "Character {CharacterId} pet-bag-rearrange no-op: source equals destination (slot {Slot})",
                characterId, sourceBagSlot);
            return GenericActionResult.Succeeded;
        }

        var sourceBagItemId = PetBagItemTransferPolicy.IsValidBagSlot(sourceBagSlot)
            ? state.GetPetBagSlot((byte)sourceBagSlot)
            : null;
        var destinationBagItemId = PetBagItemTransferPolicy.IsValidBagSlot(destinationBagSlot)
            ? state.GetPetBagSlot((byte)destinationBagSlot)
            : null;

        byte sourceItemSort = 0;
        if (sourceBagItemId is { } sourceItemId &&
            worldData.ItemsById.TryGetValue(sourceItemId, out var itemDefinition))
            sourceItemSort = itemDefinition.Item.Sort;

        var petEquipped = IsPetEquipped(state);

        var resolved = PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag(sourceBagSlot, destinationBagSlot,
            sourceBagItemId, destinationBagItemId, sourceItemSort, petEquipped, petBagUpperHalfEntitlementActive);

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} pet-bag-rearrange disconnect: policy rejected ({Outcome})", characterId,
                resolved.Outcome);
            return GenericActionResult.Aborted;
        }

        await petBag.RearrangeAsync(characterId, (byte)sourceBagSlot, (byte)destinationBagSlot, cancellationToken);

        var command = new PetBagZoneCommand(characterId,
            ImmutableArray.Create(
                new PetBagSlotWrite((byte)sourceBagSlot, resolved.NewSourcePetBagItemId),
                new PetBagSlotWrite((byte)destinationBagSlot, resolved.NewDestinationPetBagItemId)),
            null);

        if (!await zone.PostPetBagCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} pet-bag inbox full: dropped rearrange mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} pet-bag-rearrange applied: slot {SourceBagSlot} -> slot {DestBagSlot}",
            characterId, sourceBagSlot, destinationBagSlot);

        return GenericActionResult.Succeeded;
    }

    /// <summary>
    ///     C8 contract's own gate: "the pet equipment slot, equip index 8, holds an id of at least 1" -- the
    ///     real pet-validity check is dead code in legacy (only this floor test actually runs), per
    ///     <see cref="PetBagItemTransferPolicy" />'s own remarks.
    /// </summary>
    private static bool IsPetEquipped(PlayerRuntimeState state)
    {
        var petStack = state.Inventory.GetSlot(ContainerMatrix.Equipment, PetSlots.EquipmentSlot);
        return petStack is { ItemId: >= 1 };
    }

    private static byte SafeInventoryPage(int rawPage)
    {
        return rawPage is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1
            ? (byte)rawPage
            : InvalidByte;
    }

    private static ImmutableDictionary<byte, ItemStack> ApplySlotChange(
        ImmutableDictionary<byte, ItemStack> current, byte slot, ItemStack? newValue)
    {
        return newValue is { } value ? current.SetItem(slot, value) : current.Remove(slot);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
