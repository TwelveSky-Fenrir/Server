using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

/// <inheritdoc cref="IMountStateService" />
public sealed class MountStateService(
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    ILogger<MountStateService> logger)
    : IMountStateService
{
    /// <summary>Sort 5 (Delete Mount) -- a compensation grant, not a cost. Server/ts25zone/S04_MyWork02.cpp:11910.</summary>
    private const int DeleteMountContributionPointsGrant = 250;

    /// <summary>Sort 7 (Delete Rolled Attribute)'s consumed material.</summary>
    private const int AttributeDeleteItemId = 1225;

    /// <summary>
    ///     Sort 8 (Transfer Rolled Attribute)'s primary consumed material -- checked before
    ///     <see cref="AttributeTransferItemIdSecondary" />.
    /// </summary>
    private const int AttributeTransferItemIdPrimary = 8425;

    /// <summary>Sort 8 (Transfer Rolled Attribute)'s fallback consumed material.</summary>
    private const int AttributeTransferItemIdSecondary = 1226;

    private const short MountDeleteEventCode = 1;
    private const short MountAttributeDeleteEventCode = 2;

    public async ValueTask<MountStateResult> ApplyAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, int sort, int value, CancellationToken cancellationToken)
    {
        var hasAttributeDeleteMaterial = false;
        var hasAttributeTransferMaterial = false;
        var materialPage = ContainerMatrix.InventoryPage0;
        var materialSlot = (byte)0;

        // Only Sort 7/8 need an inventory scan -- every other sort resolves purely from already-loaded
        // PlayerRuntimeState fields, same "don't pay for I/O you don't need" posture as CraftPetService's
        // own per-recipe slot lookups.
        if (sort == 7)
        {
            var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
            var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);
            hasAttributeDeleteMaterial =
                TryFindItem(page0, page1, AttributeDeleteItemId, out materialPage, out materialSlot);
        }
        else if (sort == 8)
        {
            var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
            var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);
            hasAttributeTransferMaterial =
                TryFindItem(page0, page1, AttributeTransferItemIdPrimary, out materialPage, out materialSlot) ||
                TryFindItem(page0, page1, AttributeTransferItemIdSecondary, out materialPage, out materialSlot);
        }

        var context = new MountStateResolver.Context(state.AnimalIndex, state.AnimalTime, state.ActionSort,
            state.MountGarage, state.MountAccumulatedExp, state.MountRolledAttributeTotal,
            hasAttributeDeleteMaterial, hasAttributeTransferMaterial);
        var result = MountStateResolver.Resolve(sort, value, in context);

        switch (result.Kind)
        {
            case MountStateResolver.ResultKind.NoReply:
                return new MountStateResult(MountStateOutcome.NoReply);

            case MountStateResolver.ResultKind.Disconnect:
                return new MountStateResult(MountStateOutcome.Disconnect);

            case MountStateResolver.ResultKind.Select:
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex));
                return new MountStateResult(MountStateOutcome.Select);

            case MountStateResolver.ResultKind.Deselect:
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex));
                return new MountStateResult(MountStateOutcome.Deselect);

            case MountStateResolver.ResultKind.Mount:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex,
                    result.NewAnimalNumber, 0, maxLife, maxMana,
                    Broadcast: MountBroadcastKind.Mount));
                return new MountStateResult(MountStateOutcome.Mount);
            }

            case MountStateResolver.ResultKind.Dismount:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex,
                    0, 0, maxLife, maxMana,
                    Broadcast: MountBroadcastKind.Dismount));
                return new MountStateResult(MountStateOutcome.Dismount);
            }

            case MountStateResolver.ResultKind.DeleteMount:
                await ApplyDeleteMountAsync(zone, state, characterId, accountId, result.GarageSlot,
                    cancellationToken);
                return new MountStateResult(MountStateOutcome.DeleteMount);

            case MountStateResolver.ResultKind.DeleteAttribute:
                await ApplyDeleteAttributeAsync(zone, state, characterId, accountId, result.GarageSlot,
                    result.StatSlotIndex, materialPage, materialSlot, cancellationToken);
                return new MountStateResult(MountStateOutcome.DeleteAttribute);

            default:
                return new MountStateResult(MountStateOutcome.NoReply);
        }
    }

    /// <summary>
    ///     Sort 5 success: +250 CP (compensation, not a cost -- see <see cref="DeleteMountContributionPointsGrant" />'s
    ///     own remarks) mirrored through the same <see cref="Zone.PostTribeProgressCommandAndWaitAsync" />
    ///     pathway <c>CraftLegendaryPetService</c> already uses for its own CP debit, plus the garage-slot
    ///     clear mirrored through <see cref="Zone.PostMountCommandAndWaitAsync" />. Réf. C++ :
    ///     Server/ts25zone/S04_MyWork02.cpp:11907-11919.
    /// </summary>
    private async ValueTask ApplyDeleteMountAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, int garageSlot, CancellationToken cancellationToken)
    {
        var newContributionPoints = state.ContributionPoints + DeleteMountContributionPointsGrant;

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, newContributionPoints), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped CP mirror for character {CharacterId} after mount delete",
                zone.MapId, characterId);

        if (!await zone.PostMountCommandAndWaitAsync(
                new MountZoneCommand(characterId, -1, DeleteGarageSlot: garageSlot),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} mount inbox full: dropped garage-slot clear mirror for character {CharacterId} after mount delete",
                zone.MapId, characterId);

        // Logged after both mirrors are posted -- see this class's own note on EventLogCategory.MountAttribute
        // for why deltaMoney carries the CP delta here rather than actual wallet money.
        await eventLog.LogAsync(MountDeleteEventCode, EventLogCategory.MountAttribute, accountId, characterId,
            null, null, null, DeleteMountContributionPointsGrant, null, null, null, 1, null, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} deleted mount (garage slot {GarageSlot}), granted {Cp} CP", characterId,
            garageSlot, DeleteMountContributionPointsGrant);
    }

    /// <summary>
    ///     Sort 7 success: zeroes the addressed rolled-attribute slot, consumes one unit of item 1225 (found by
    ///     <paramref name="materialPage" />/<paramref name="materialSlot" />, already resolved by
    ///     <see cref="ApplyAsync" /> before the resolver ran), and mirrors both. Réf. C++ :
    ///     Server/ts25zone/S04_MyWork02.cpp:11977-11991.
    /// </summary>
    private async ValueTask ApplyDeleteAttributeAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, int garageSlot, int statSlotIndex, byte materialPage, byte materialSlot,
        CancellationToken cancellationToken)
    {
        var container = state.Inventory.GetContainer(materialPage);
        var projected = ConsumeOne(container, materialSlot);

        await characters.ReplaceContainerAsync(characterId, materialPage, ToTvps(projected), cancellationToken);

        await eventLog.LogAsync(MountAttributeDeleteEventCode, EventLogCategory.MountAttribute, accountId,
            characterId, null, null, null, null, null, AttributeDeleteItemId, 1, 1, null, cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(materialPage, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped mount-attribute-delete item mirror for character {CharacterId}",
                zone.MapId, characterId);

        if (!await zone.PostMountCommandAndWaitAsync(
                new MountZoneCommand(characterId, AttributeDeleteGarageSlot: garageSlot,
                    AttributeDeleteStatSlotIndex: statSlotIndex), cancellationToken))
            logger.LogError(
                "Zone {MapId} mount inbox full: dropped attribute-slot clear mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} deleted rolled mount attribute (garage slot {GarageSlot}, stat slot {StatSlot})",
            characterId, garageSlot, statSlotIndex + 1);
    }

    /// <summary>Port of FindInventoryItem's scan order: page 0 ascending slot, then page 1 ascending slot.</summary>
    private static bool TryFindItem(ImmutableDictionary<byte, ItemStack> page0,
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

    private static ImmutableDictionary<byte, ItemStack> ConsumeOne(ImmutableDictionary<byte, ItemStack> container,
        byte slot)
    {
        var stack = container[slot];
        var remaining = stack.Quantity - 1;
        return remaining > 0 ? container.SetItem(slot, stack with { Quantity = remaining }) : container.Remove(slot);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
