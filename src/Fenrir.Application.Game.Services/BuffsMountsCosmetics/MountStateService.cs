using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Domain.Game.Stats;
using Fenrir.Domain.Game.Stats.Context;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

public sealed class MountStateService(
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    WorldDataCache worldData,
    ILogger<MountStateService> logger)
    : IMountStateService
{
    private const int DeleteMountContributionPointsGrant = 250;

    private const int AttributeDeleteItemId = 1225;

    private const int AttributeTransferItemIdPrimary = 8425;

    private const int AttributeTransferItemIdSecondary = 1226;

    private const short MountDeleteEventCode = 1;
    private const short MountAttributeDeleteEventCode = 2;

    private const short MountAttributeConvertEventCode = 1;
    private const short MountAttributeTransferEventCode = 3;

    public async ValueTask<MountStateResult> ApplyAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, int sort, int value, CancellationToken cancellationToken)
    {
        var hasAttributeDeleteMaterial = false;
        var hasAttributeTransferMaterial = false;
        var materialPage = ContainerMatrix.InventoryPage0;
        var materialSlot = (byte)0;

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
                var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
                var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt,
                    state.StatDex, state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo,
                    state.RebirthCount, state.Level2);
                var mountedSlot = result.NewAnimalIndex - MountStateResolver.SlotCount;
                var inRange = mountedSlot is >= 0 and < MountStateResolver.SlotCount;
                var absorbValue =
                    StatCalculator.TryGetMountBaseRow(result.NewAnimalNumber, out var baseRow)
                        ? baseRow.AbsorbValue
                        : 0;
                var mountOverride = new MountContext(result.NewAnimalNumber,
                    state.AnimalAbsorbState != 0,
                    absorbValue,
                    inRange ? MountPowerCodec.EncodeSlot(state.MountRolledAttributes, mountedSlot) : 0,
                    inRange ? state.MountActivity[mountedSlot] : 0);
                var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
                    state.Buffs, ComputePetContribution(state, equipmentContainer), state,
                    mountOverride: mountOverride);

                var newLife = Math.Min(state.Life, updatedStats.MaxLife);
                var newMana = Math.Min(state.Mana, updatedStats.MaxMana);

                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex,
                    result.NewAnimalNumber, 0, newLife, newMana, updatedStats,
                    MountBroadcastKind.Mount));
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
                return await ApplyDeleteAttributeAsync(zone, state, characterId, accountId, sort,
                    result.GarageSlot, result.StatSlotIndex, materialPage, materialSlot, cancellationToken);

            case MountStateResolver.ResultKind.Convert:
                return await ApplyConvertAsync(zone, state, characterId, accountId, sort, result.GarageSlot,
                    cancellationToken);

            case MountStateResolver.ResultKind.Transfer:
                return await ApplyTransferAsync(zone, state, characterId, accountId, sort, result.GarageSlot,
                    result.StatSlotIndex, materialPage, materialSlot, cancellationToken);

            default:
                return new MountStateResult(MountStateOutcome.NoReply);
        }
    }

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

        await eventLog.LogAsync(MountDeleteEventCode, EventLogCategory.MountAttribute, accountId, characterId,
            null, null, null, DeleteMountContributionPointsGrant, null, null, null, 1, null, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} deleted mount (garage slot {GarageSlot}), granted {Cp} CP", characterId,
            garageSlot, DeleteMountContributionPointsGrant);
    }

    private async ValueTask<MountStateResult> ApplyDeleteAttributeAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, int sort, int garageSlot, int statSlotIndex, byte materialPage,
        byte materialSlot, CancellationToken cancellationToken)
    {
        var power = MountPowerCodec.EncodeSlot(state.MountRolledAttributes, garageSlot);

        var newPower = MountAttributeRoller.Delete(power, statSlotIndex + 1);

        await PostRolledAttributeMirrorAsync(zone, characterId, garageSlot, newPower, null, cancellationToken);

        await eventLog.LogAsync(MountAttributeDeleteEventCode, EventLogCategory.MountAttribute, accountId,
            characterId, null, null, null, null, null, AttributeDeleteItemId, 1, 1, null, cancellationToken);

        var container = state.Inventory.GetContainer(materialPage);
        var projected = ConsumeOne(container, materialSlot);
        await characters.ReplaceContainerAsync(characterId, materialPage, ToTvps(projected), cancellationToken);

        state.Session.Send(new MountStateResponse { Sort = sort, Value = newPower });
        await RecomputeAndPostVitalsAsync(zone, state, characterId, cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(materialPage, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped mount-attribute-delete item mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} deleted rolled mount attribute (garage slot {GarageSlot}, stat slot {StatSlot}), new power {Power}",
            characterId, garageSlot, statSlotIndex + 1, newPower);
        return new MountStateResult(MountStateOutcome.NoReply);
    }

    private async ValueTask<MountStateResult> ApplyConvertAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, int sort, int garageSlot, CancellationToken cancellationToken)
    {
        var power = MountPowerCodec.EncodeSlot(state.MountRolledAttributes, garageSlot);
        var roll = MountAttributeRoller.Convert(power, SystemRandomSource.Instance);
        if (!roll.Applied)
            return new MountStateResult(MountStateOutcome.Disconnect);

        await PostRolledAttributeMirrorAsync(zone, characterId, garageSlot, roll.NewPower, garageSlot,
            cancellationToken);

        await eventLog.LogAsync(MountAttributeConvertEventCode, EventLogCategory.MountAttribute, accountId, characterId,
            null, null, null, null, null, null, null, 1, null, cancellationToken);

        state.Session.Send(new MountStateResponse { Sort = sort, Value = roll.NewPower });
        await RecomputeAndPostVitalsAsync(zone, state, characterId, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} converted mount attribute (garage slot {GarageSlot}), new power {Power}",
            characterId, garageSlot, roll.NewPower);
        return new MountStateResult(MountStateOutcome.NoReply);
    }

    private async ValueTask<MountStateResult> ApplyTransferAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, int sort, int garageSlot, int statSlotIndex, byte materialPage, byte materialSlot,
        CancellationToken cancellationToken)
    {
        var power = MountPowerCodec.EncodeSlot(state.MountRolledAttributes, garageSlot);

        var roll = MountAttributeRoller.Transfer(power, statSlotIndex + 1, SystemRandomSource.Instance);
        if (!roll.Applied)
            return new MountStateResult(MountStateOutcome.Disconnect);

        await PostRolledAttributeMirrorAsync(zone, characterId, garageSlot, roll.NewPower, null, cancellationToken);

        await eventLog.LogAsync(MountAttributeTransferEventCode, EventLogCategory.MountAttribute, accountId,
            characterId, null, null, null, null, null, null, null, 1, null, cancellationToken);

        var container = state.Inventory.GetContainer(materialPage);
        var projected = ConsumeOne(container, materialSlot);
        await characters.ReplaceContainerAsync(characterId, materialPage, ToTvps(projected), cancellationToken);

        state.Session.Send(new MountStateResponse { Sort = sort, Value = roll.NewPower });
        await RecomputeAndPostVitalsAsync(zone, state, characterId, cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(materialPage, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped mount-transfer material mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} transferred mount attribute (garage slot {GarageSlot}, source stat slot {Slot}), new power {Power}",
            characterId, garageSlot, statSlotIndex + 1, roll.NewPower);
        return new MountStateResult(MountStateOutcome.NoReply);
    }

    private async ValueTask PostRolledAttributeMirrorAsync(Zone zone, int characterId, int garageSlot, int newPower,
        int? resetAccumulatedExpGarageSlot, CancellationToken cancellationToken)
    {
        if (!await zone.PostMountCommandAndWaitAsync(
                new MountZoneCommand(characterId, RolledAttributeGarageSlot: garageSlot,
                    RolledAttributeNewPower: newPower,
                    ResetAccumulatedExpGarageSlot: resetAccumulatedExpGarageSlot),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} mount inbox full: dropped rolled-attribute mirror for character {CharacterId} (garage slot {GarageSlot})",
                zone.MapId, characterId, garageSlot);
    }

    private async ValueTask RecomputeAndPostVitalsAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount, state.Level2);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            ComputePetContribution(state, equipmentContainer), state);

        var newLife = Math.Min(state.Life, updatedStats.MaxLife);
        var newMana = Math.Min(state.Mana, updatedStats.MaxMana);

        if (!await zone.PostMountCommandAndWaitAsync(
                new MountZoneCommand(characterId, Life: newLife, Mana: newMana, UpdatedStats: updatedStats),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} mount inbox full: dropped stat recompute mirror for character {CharacterId} after mount attribute change",
                zone.MapId, characterId);
    }

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

    private PetStatContribution ComputePetContribution(PlayerRuntimeState state,
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer)
    {
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        return PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity, worldData.ItemsById);
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
