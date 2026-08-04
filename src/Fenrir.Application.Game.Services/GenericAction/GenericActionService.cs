using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.WarPoint;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.Stats;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;
using Fenrir.Domain.Game.Stats;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.GenericAction;

public sealed class GenericActionService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    QuestCatalog questCatalog,
    PartyRegistry partyRegistry,
    IEventLogRepository eventLog,
    IAccountVaultRepository accountVault,
    TradeRegistry trades,
    ZoneRegistry zoneRegistry,
    ILogger<GenericActionService> logger,
    IWarPointShopService? warPointShop = null,
    DuelRegistry? duels = null)
    : IGenericActionService
{
    private const long MaximumMoneyBalance = 2_000_000_000;

    private const short TimeExchangeEventCode = 1;

    private const byte TimeExchangeOutcome = 1;

    private const short VaultTransferDepositEventCode = 1;

    private const short VaultTransferWithdrawEventCode = 2;
    private const byte VaultTransferOutcome = 1;

    private const int TeacherPointsPerPlayTimeMinute = 694;

    private const int PetExperiencePerPlayTimeMinute = 400;

    private const int IdleActionSort = 1;

    public async ValueTask<GenericActionResult> MoveContainerAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        switch (GmStubCommandResolver.Evaluate(sort, state.UserSort))
        {
            case GmStubCommandOutcome.NotAuthorized:
                logger.LogDebug(
                    "Character {CharacterId} attempted GM stub sort {Sort} without the {RequiredTier} tier: disconnecting without a response",
                    characterId, sort, GmStubCommandResolver.RequiredTier);
                return GenericActionResult.Aborted;

            case GmStubCommandOutcome.NoOpFailure:
                logger.LogInformation(
                    "Character {CharacterId} invoked GM stub sort {Sort}: body is empty in the shipped legacy build, answering failure with the payload echoed back",
                    characterId, sort);
                return GenericActionResult.Failed;

            case GmStubCommandOutcome.NotAStubCommand:
                break;
        }

        switch (GmLevel2CommandResolver.Evaluate(sort, MeetsGmTier(state, GmLevel2CommandResolver.RequiredTier)))
        {
            case GmLevel2CommandOutcome.NotAuthorized:
                logger.LogDebug(
                    "Character {CharacterId} attempted GM command {Command} (sort {Sort}) without the {RequiredTier} tier: disconnecting without a response",
                    characterId, GmLevel2CommandResolver.CommandName, GmLevel2CommandResolver.Sort,
                    GmLevel2CommandResolver.RequiredTier);
                return GenericActionResult.Aborted;

            case GmLevel2CommandOutcome.Refused:
                logger.LogInformation(
                    "Character {CharacterId} invoked GM command {Command} (sort {Sort}): body is neutralized in both shipped legacy builds, answering failure with the payload echoed back",
                    characterId, GmLevel2CommandResolver.CommandName, GmLevel2CommandResolver.Sort);
                return GenericActionResult.Failed;

            case GmLevel2CommandOutcome.NotThisCommand:
                break;
        }

        switch (GmDeleteItemCommandResolver.Evaluate(sort,
                    MeetsGmTier(state, GmDeleteItemCommandResolver.RequiredTier)))
        {
            case GmDeleteItemCommandOutcome.NotAuthorized:
                logger.LogDebug(
                    "Character {CharacterId} attempted GM command {Command} (sort {Sort}) without the {RequiredTier} tier: disconnecting without a response",
                    characterId, GmDeleteItemCommandResolver.CommandName, GmDeleteItemCommandResolver.Sort,
                    GmDeleteItemCommandResolver.RequiredTier);
                return GenericActionResult.Aborted;

            case GmDeleteItemCommandOutcome.Refused:
                logger.LogInformation(
                    "Character {CharacterId} invoked GM command {Command} (sort {Sort}): body is empty in both shipped legacy builds, answering failure with no state change",
                    characterId, GmDeleteItemCommandResolver.CommandName, GmDeleteItemCommandResolver.Sort);
                return GenericActionResult.Failed;

            case GmDeleteItemCommandOutcome.NotThisCommand:
                break;
        }

        switch (GmMonsterKillCommandResolver.Evaluate(sort,
                    MeetsGmTier(state, GmMonsterKillCommandResolver.RequiredTier)))
        {
            case GmMonsterKillCommandOutcome.NotAuthorized:
                logger.LogDebug(
                    "Character {CharacterId} attempted GM command {Command} (sort {Sort}) without the {RequiredTier} tier: disconnecting without a response",
                    characterId, GmMonsterKillCommandResolver.CommandName, GmMonsterKillCommandResolver.Sort,
                    GmMonsterKillCommandResolver.RequiredTier);
                return GenericActionResult.Aborted;

            case GmMonsterKillCommandOutcome.Refused:
                logger.LogInformation(
                    "Character {CharacterId} invoked GM command {Command} (sort {Sort}): the case is an empty stub in both shipped legacy builds, no monster is affected, answering failure with the payload echoed back",
                    characterId, GmMonsterKillCommandResolver.CommandName, GmMonsterKillCommandResolver.Sort);
                return GenericActionResult.Failed;

            case GmMonsterKillCommandOutcome.NotThisCommand:
                break;
        }

        switch (Zone124DuelReadyResolver.Evaluate(sort, MeetsGmTier(state, Zone124DuelReadyResolver.RequiredTier),
                    zone.MapId))
        {
            case Zone124DuelReadyOutcome.NotAuthorized:
                logger.LogDebug(
                    "Character {CharacterId} attempted GM command {Command} (sort {Sort}) without the {RequiredTier} tier: disconnecting without a response",
                    characterId, Zone124DuelReadyResolver.CommandName, Zone124DuelReadyResolver.Sort,
                    Zone124DuelReadyResolver.RequiredTier);
                return GenericActionResult.Aborted;

            case Zone124DuelReadyOutcome.WrongMap:
                logger.LogInformation(
                    "Character {CharacterId} invoked GM command {Command} (sort {Sort}) from map {MapId}: only map {RequiredMapId} musters, refused",
                    characterId, Zone124DuelReadyResolver.CommandName, Zone124DuelReadyResolver.Sort, zone.MapId,
                    Zone124DuelReadyResolver.MapId);
                return GenericActionResult.Failed;

            case Zone124DuelReadyOutcome.Authorized:
                return await MusterZone124DuelReadyAsync(zone, characterId, cancellationToken);

            case Zone124DuelReadyOutcome.NotThisCommand:
                break;
        }

        switch (Zone124DuelStartResolver.Evaluate(sort, MeetsGmTier(state, Zone124DuelStartResolver.RequiredTier),
                    zone.MapId))
        {
            case Zone124DuelStartOutcome.NotAuthorized:
                logger.LogDebug(
                    "Character {CharacterId} attempted GM command {Command} (sort {Sort}) without the {RequiredTier} tier: disconnecting without a response",
                    characterId, Zone124DuelStartResolver.CommandName, Zone124DuelStartResolver.Sort,
                    Zone124DuelStartResolver.RequiredTier);
                return GenericActionResult.Aborted;

            case Zone124DuelStartOutcome.WrongMap:
                logger.LogInformation(
                    "Character {CharacterId} invoked GM command {Command} (sort {Sort}) from map {MapId}: only map {RequiredMapId} runs the mass duel, refused",
                    characterId, Zone124DuelStartResolver.CommandName, Zone124DuelStartResolver.Sort, zone.MapId,
                    Zone124DuelStartResolver.MapId);
                return GenericActionResult.Failed;

            case Zone124DuelStartOutcome.Authorized:
                return await StartZone124DuelAsync(zone, characterId, cancellationToken);

            case Zone124DuelStartOutcome.NotThisCommand:
                break;
        }

        switch (Zone124DuelEndResolver.Evaluate(sort, MeetsGmTier(state, Zone124DuelEndResolver.RequiredTier),
                    zone.MapId))
        {
            case Zone124DuelEndOutcome.NotAuthorized:
                logger.LogDebug(
                    "Character {CharacterId} attempted GM command {Command} (sort {Sort}) without the {RequiredTier} tier: disconnecting without a response",
                    characterId, Zone124DuelEndResolver.CommandName, Zone124DuelEndResolver.Sort,
                    Zone124DuelEndResolver.RequiredTier);
                return GenericActionResult.Aborted;

            case Zone124DuelEndOutcome.WrongMap:
                logger.LogInformation(
                    "Character {CharacterId} invoked GM command {Command} (sort {Sort}) from map {MapId}: only map {RequiredMapId} runs the mass duel, refused",
                    characterId, Zone124DuelEndResolver.CommandName, Zone124DuelEndResolver.Sort, zone.MapId,
                    Zone124DuelEndResolver.MapId);
                return GenericActionResult.Failed;

            case Zone124DuelEndOutcome.Authorized:
                return EndZone124Duel(zone, characterId);

            case Zone124DuelEndOutcome.NotThisCommand:
                break;
        }

        switch (Zone124DuelOutResolver.Evaluate(sort, MeetsGmTier(state, Zone124DuelOutResolver.RequiredTier),
                    zone.MapId))
        {
            case Zone124DuelOutOutcome.NotAuthorized:
                logger.LogDebug(
                    "Character {CharacterId} attempted GM command {Command} (sort {Sort}) without the {RequiredTier} tier: disconnecting without a response",
                    characterId, Zone124DuelOutResolver.CommandName, Zone124DuelOutResolver.Sort,
                    Zone124DuelOutResolver.RequiredTier);
                return GenericActionResult.Aborted;

            case Zone124DuelOutOutcome.WrongMap:
                logger.LogInformation(
                    "Character {CharacterId} invoked GM command {Command} (sort {Sort}) from map {MapId}: only map {RequiredMapId} runs the mass duel, refused",
                    characterId, Zone124DuelOutResolver.CommandName, Zone124DuelOutResolver.Sort, zone.MapId,
                    Zone124DuelOutResolver.MapId);
                return GenericActionResult.Failed;

            case Zone124DuelOutOutcome.Authorized:
                return await EvacuateZone124DuelAsync(zone, characterId, cancellationToken);

            case Zone124DuelOutOutcome.NotThisCommand:
                break;
        }

        if (!ContainerMatrix.IsImplementedContainerMoveSort(sort))
        {
            logger.LogDebug("Character {CharacterId} container-move rejected: sort {Sort} not implemented",
                characterId, sort);
            return GenericActionResult.Failed;
        }

        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} container-move aborted: malformed DefaultPData payload (sort {Sort})",
                characterId, sort);
            return GenericActionResult.Aborted;
        }

        var isEquipmentTransfer = sort is 210 or 213;
        var dragDropOutcome = InventoryEquipDragDropTransferGate.Evaluate(sort, move, state.InventoryDate);
        if (dragDropOutcome != InventoryEquipDragDropTransferGate.Outcome.Valid)
        {
            logger.LogInformation(
                "Character {CharacterId} container-move rejected: {DragDropOutcome} (sort {Sort}, {Page1}:{Index1} -> {Page2}:{Index2})",
                characterId, dragDropOutcome, sort, move.Page1, move.Index1, move.Page2, move.Index2);
            return isEquipmentTransfer ? GenericActionResult.Aborted : GenericActionResult.Failed;
        }

        if (sort == 208 && !IsInventoryToInventoryRequestValid(move, state.InventoryDate))
        {
            logger.LogInformation(
                "Character {CharacterId} container-move aborted: inventory-to-inventory gate ({Page1}:{Index1} -> {Page2}:{Index2}, grid {XPost2},{YPost2}, InventoryDate {InventoryDate})",
                characterId, move.Page1, move.Index1, move.Page2, move.Index2, move.XPost2, move.YPost2,
                state.InventoryDate);
            return GenericActionResult.Failed;
        }

        if (!ContainerMatrix.TryResolveContainers(sort, move.Page1, move.Page2, out var fromContainer,
                out var toContainer))
        {
            logger.LogDebug(
                "Character {CharacterId} container-move rejected: unresolvable containers (sort {Sort}, page1 {Page1}, page2 {Page2})",
                characterId, sort, move.Page1, move.Page2);
            return isEquipmentTransfer ? GenericActionResult.Aborted : GenericActionResult.Failed;
        }

        var sourceStack = ContainerMatrix.IsValidSlot(fromContainer, move.Index1)
            ? state.Inventory.GetSlot(fromContainer, (byte)move.Index1)
            : null;
        var destinationStack = ContainerMatrix.IsValidSlot(toContainer, move.Index2)
            ? state.Inventory.GetSlot(toContainer, (byte)move.Index2)
            : null;

        if (sourceStack is not { } sourceItem)
        {
            logger.LogInformation(
                "Character {CharacterId} container-move rejected: source slot empty ({FromContainer}:{Index1})",
                characterId, fromContainer, move.Index1);
            return isEquipmentTransfer ? GenericActionResult.Aborted : GenericActionResult.Failed;
        }

        var touchesEquipment = toContainer == ContainerMatrix.Equipment || fromContainer == ContainerMatrix.Equipment;

        if (!worldData.ItemsById.TryGetValue(sourceItem.ItemId, out var sourceDefinition))
        {
            logger.LogInformation(
                "Character {CharacterId} container-move rejected: item {ItemId} is absent from the catalog",
                characterId, sourceItem.ItemId);
            return isEquipmentTransfer ? GenericActionResult.Aborted : GenericActionResult.Failed;
        }

        if (isEquipmentTransfer && destinationStack is not null)
        {
            logger.LogInformation(
                "Character {CharacterId} equipment transfer aborted: destination {ToContainer}:{Index2} is occupied",
                characterId, toContainer, move.Index2);
            return GenericActionResult.Aborted;
        }

        if (toContainer == ContainerMatrix.Equipment)
        {
            var equipOutcome = EquipItemValidationGate.Evaluate(
                EquipItemValidationGate.EquipCandidate.FromRow(sourceDefinition.Item),
                state.PreviousTribe, move.Index2, state.Level + state.Level2, state.RebirthCount);

            if (equipOutcome != EquipItemValidationGate.Outcome.Success)
            {
                logger.LogInformation(
                    "Character {CharacterId} equip rejected by validation gate: outcome {EquipOutcome}, item {ItemId}",
                    characterId, equipOutcome, sourceItem.ItemId);
                return isEquipmentTransfer ? GenericActionResult.Aborted : GenericActionResult.Failed;
            }
        }

        if (TouchesLiveTradeReservation(characterId, fromContainer, move.Index1, toContainer, move.Index2))
        {
            logger.LogInformation(
                "Character {CharacterId} container-move rejected: sort {Sort} touches an inventory slot ({FromContainer}:{Index1} or {ToContainer}:{Index2}) reserved by a live trade offer",
                characterId, sort, fromContainer, move.Index1, toContainer, move.Index2);
            return GenericActionResult.Failed;
        }

        if (touchesEquipment && state.ActionSort != IdleActionSort)
        {
            logger.LogInformation(
                "Character {CharacterId} equip/unequip rejected: not in idle pose (ActionSort {ActionSort})",
                characterId, state.ActionSort);
            return GenericActionResult.Failed;
        }

        if (!isEquipmentTransfer && (move.XPost2 is < 0 or > 7 || move.YPost2 is < 0 or > 7))
        {
            logger.LogInformation(
                "Character {CharacterId} container-move aborted: invalid destination grid position ({XPost2},{YPost2})",
                characterId, move.XPost2, move.YPost2);
            return GenericActionResult.Aborted;
        }

        var sourceItemSort = sourceDefinition.Item.Sort;
        var hasLegacySingleQuantity = sourceItemSort switch
        {
            ItemQuantityPolicy.PetSort => sourceItem.Quantity is >= ItemQuantityPolicy.MinStackQuantity and <=
                ItemQuantityPolicy.MaxPetActivity,
            _ when ItemQuantityPolicy.CarriesNoQuantity(sourceItemSort) => sourceItem.Quantity is 0 or 1,
            _ => false
        };
        var requestedQuantity = move.Quantity1 == 0 && hasLegacySingleQuantity
            ? 1
            : move.Quantity1;

        var resolved = ContainerMatrix.ResolveMove(fromContainer, move.Index1, requestedQuantity, toContainer,
            move.Index2, (byte)move.XPost2, (byte)move.YPost2, sourceStack, destinationStack,
            sourceItemSort);

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} container-move rejected by policy (sort {Sort}, {FromContainer}:{Index1} -> {ToContainer}:{Index2})",
                characterId, sort, fromContainer, move.Index1, toContainer, move.Index2);
            return isEquipmentTransfer ? GenericActionResult.Aborted : GenericActionResult.Failed;
        }

        if (resolved.Outcome == ContainerMatrix.MoveOutcome.NoOp)
            return GenericActionResult.Succeeded;

        if (resolved.NewDestination is { } movedStack)
        {
            if (fromContainer == ContainerMatrix.Equipment && move.Index1 == PetSlots.EquipmentSlot)
                movedStack = PetItemState.WithState(movedStack, state.PetGrowth, state.PetActivity);

            var movesPet = (fromContainer == ContainerMatrix.Equipment &&
                            move.Index1 == PetSlots.EquipmentSlot) ||
                           (toContainer == ContainerMatrix.Equipment &&
                            move.Index2 == PetSlots.EquipmentSlot);
            if (touchesEquipment && !movesPet)
                movedStack = FilterSocketState(movedStack, sourceDefinition.Item.Sort, sourceDefinition.Item.Type);

            resolved = resolved with { NewDestination = movedStack };
        }

        var projected = ContainerMatrix.ApplyMove(resolved, fromContainer, move.Index1,
            state.Inventory.GetContainer(fromContainer), toContainer, move.Index2,
            state.Inventory.GetContainer(toContainer));

        EffectiveStats? updatedStats = null;
        int? petGrowth = null;
        byte? petActivity = null;
        if (fromContainer == ContainerMatrix.Equipment || toContainer == ContainerMatrix.Equipment)
        {
            var equipmentContainer = fromContainer == ContainerMatrix.Equipment ? projected.From : projected.To;
            var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt,
                state.StatDex, state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo,
                state.RebirthCount, state.Level2);

            var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
                ? petStack.ItemId
                : 0;
            if ((fromContainer == ContainerMatrix.Equipment && move.Index1 == PetSlots.EquipmentSlot) ||
                (toContainer == ContainerMatrix.Equipment && move.Index2 == PetSlots.EquipmentSlot))
            {
                petGrowth = petStack is { } nextPet ? PetItemState.Growth(nextPet) : 0;
                petActivity = petStack is { } nextPetActivity ? PetItemState.Activity(nextPetActivity) : (byte)0;
            }

            var effectivePetGrowth = petGrowth ?? state.PetGrowth;
            var effectivePetActivity = petActivity ?? state.PetActivity;
            var petContribution = PetGrowthCalculator.Compute(petItemId, effectivePetGrowth, effectivePetActivity,
                worldData.ItemsById);

            updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
                petContribution, state);
        }

        if (toContainer == fromContainer)
            await characters.ReplaceContainerAsync(characterId, fromContainer, ToTvps(projected.From),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, fromContainer, ToTvps(projected.From),
                toContainer, ToTvps(projected.To), cancellationToken);

        var containers = toContainer == fromContainer
            ? ImmutableArray.Create(new InventoryContainerSnapshot(fromContainer, projected.From))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot(fromContainer, projected.From),
                new InventoryContainerSnapshot(toContainer, projected.To));

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(characterId, containers, updatedStats,
                    RecomputeCombatPoseAfterEquip: touchesEquipment,
                    ClearEffectsAfterWeaponUnequip: fromContainer == ContainerMatrix.Equipment &&
                                                    move.Index1 == EquipmentSlots.WeaponSlot,
                    PetGrowth: petGrowth, PetActivity: petActivity),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped container-move mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} container-move applied: sort {Sort}, {FromContainer}:{Index1} -> {ToContainer}:{Index2}",
            characterId, sort, fromContainer, move.Index1, toContainer, move.Index2);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> PickupGroundItemAsync(byte[] data, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} ground-item pickup aborted: malformed DefaultPData payload", characterId);
            return GenericActionResult.Aborted;
        }

        if (move.Page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)move.Page2, move.Index2) ||
            move.XPost2 is < 0 or > 7 || move.YPost2 is < 0 or > 7)
        {
            logger.LogInformation(
                "Character {CharacterId} ground-item pickup aborted: invalid destination slot ({Page2}:{Index2})",
                characterId, move.Page2, move.Index2);
            return GenericActionResult.Aborted;
        }

        if (!RentedInventoryPageGate.IsPageAccessible(move.Page2, state.InventoryDate, GameDate.Today()))
        {
            logger.LogInformation(
                "Character {CharacterId} ground-item pickup aborted: rented inventory page {DestinationPage} expired (InventoryDate {InventoryDate})",
                characterId, move.Page2, state.InventoryDate);
            return GenericActionResult.Aborted;
        }

        if (move.Page1 is < 0 or >= InventoryToWorldDropPolicy.GroundItemCapacity)
        {
            logger.LogInformation(
                "Character {CharacterId} ground-item pickup aborted: invalid ground-item slot {ServerIndex}",
                characterId, move.Page1);
            return GenericActionResult.Aborted;
        }

        var claimantPartyName = PartyIdentityResolver.ResolveCurrentPartyName(partyRegistry, characterId,
            state.Name, memberId => zone.TryGetPlayer(memberId, out var member) ? member?.Name : null);

        var claimOutcome = zone.TryReserveGroundItem(move.Page1, unchecked((uint)move.Index1), state.Name,
            claimantPartyName, state.PosX, state.PosY, state.PosZ, out var groundItem);

        if (claimOutcome != GroundItemClaimOutcome.Success || groundItem is null)
        {
            logger.LogDebug(
                "Character {CharacterId} ground-item pickup rejected: claim outcome {ClaimOutcome}", characterId,
                claimOutcome);
            return GenericActionResult.Failed;
        }

        var reservationHeld = true;

        try
        {
            if (!worldData.ItemsById.TryGetValue(groundItem.ItemId, out var itemDefinition))
            {
                logger.LogInformation(
                    "Character {CharacterId} ground-item pickup aborted: item {ItemId} not found in catalog",
                    characterId, groundItem.ItemId);
                return GenericActionResult.Aborted;
            }

            var destinationContainer = (byte)move.Page2;
            var destinationSlot = (byte)move.Index2;
            var existingStack = state.Inventory.GetSlot(destinationContainer, destinationSlot);

            var resolved = GroundItemPickupPolicy.Resolve(itemDefinition, groundItem, existingStack,
                (byte)move.XPost2, (byte)move.YPost2);
            if (!resolved.Succeeded)
            {
                logger.LogDebug(
                    "Character {CharacterId} ground-item pickup rejected by policy (item {ItemId})", characterId,
                    groundItem.ItemId);
                return GenericActionResult.Failed;
            }

            if (resolved.Outcome == GroundItemPickupPolicy.Outcome.Money)
            {
                await characters.AdjustMoneyAsync(characterId, resolved.MoneyAmount, 0, cancellationToken);

                if (!zone.TryFinalizeGroundItemReservation(groundItem.ServerIndex, groundItem.UniqueNumber))
                {
                    logger.LogError(
                        "Character {CharacterId} ground-item money pickup could not finalize reserved slot {ServerIndex}",
                        characterId, groundItem.ServerIndex);
                    return GenericActionResult.Failed;
                }

                reservationHeld = false;
                logger.LogInformation(
                    "Character {CharacterId} ground-item pickup applied: money +{MoneyAmount}", characterId,
                    resolved.MoneyAmount);

                return GenericActionResult.Succeeded;
            }

            var originalContainer = state.Inventory.GetContainer(destinationContainer);
            var projectedContainer = originalContainer.SetItem(destinationSlot, resolved.NewSlot!.Value);

            await characters.ReplaceContainerAsync(characterId, destinationContainer, ToTvps(projectedContainer),
                cancellationToken);

            var containers =
                ImmutableArray.Create(new InventoryContainerSnapshot(destinationContainer, projectedContainer));
            if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                    new InventoryZoneCommand(characterId, containers, null), cancellationToken)).Kind !=
                ZoneCommandResultKind.Applied || state.Inventory.GetSlot(destinationContainer, destinationSlot) !=
                resolved.NewSlot)
            {
                await characters.ReplaceContainerAsync(characterId, destinationContainer, ToTvps(originalContainer),
                    cancellationToken);
                logger.LogWarning(
                    "Zone {MapId} did not apply the ground-item pickup mirror for character {CharacterId}; restored the persisted inventory container",
                    zone.MapId, characterId);
                return GenericActionResult.Failed;
            }

            try
            {
                await eventLog.LogGroundItemGainAsync(accountId, characterId, groundItem.ItemId,
                    resolved.NewSlot!.Value.Quantity, itemDefinition.Item.Type, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Character {CharacterId} ground-item pickup completed without an event-log record", characterId);
            }

            if (!zone.TryFinalizeGroundItemReservation(groundItem.ServerIndex, groundItem.UniqueNumber))
            {
                logger.LogError(
                    "Character {CharacterId} ground-item pickup could not finalize reserved slot {ServerIndex}",
                    characterId, groundItem.ServerIndex);
                return GenericActionResult.Failed;
            }

            reservationHeld = false;

            var notifyQuestProgress = false;
            if (state.QuestActiveFlag == 1 && state.QuestSort == 2 && state.QuestTargetPhase == groundItem.ItemId)
            {
                bool HasItem(int itemId)
                {
                    return state.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values
                               .Any(s => s.ItemId == itemId) ||
                           state.Inventory.GetContainer(ContainerMatrix.InventoryPage1).Values
                               .Any(s => s.ItemId == itemId);
                }

                var progress = new QuestProgress(state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort,
                    state.QuestTargetPhase, state.QuestKillCounter);
                if (QuestStateMachine.ComputePresentState(progress, state.Tribe, state.Level, questCatalog,
                        HasItem) == QuestStateMachine.StateInProgress)
                    notifyQuestProgress = true;
            }

            logger.LogInformation(
                "Character {CharacterId} ground-item pickup applied: item {ItemId} -> {DestinationContainer}:{DestinationSlot}",
                characterId, groundItem.ItemId, destinationContainer, destinationSlot);

            return new GenericActionResult(GenericActionStatus.Succeeded, notifyQuestProgress);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Character {CharacterId} ground-item pickup failed before finalization", characterId);
            return GenericActionResult.Failed;
        }
        finally
        {
            if (reservationHeld)
                zone.ReleaseGroundItemReservation(groundItem.ServerIndex, groundItem.UniqueNumber);
        }
    }

    public async ValueTask<GenericActionResult> PayTeleportTollAsync(byte[] data, int characterId,
        CancellationToken cancellationToken)
    {
        if (!TeleportTollData.TryRead(data, out var toll))
        {
            logger.LogInformation(
                "Character {CharacterId} teleport-toll aborted: malformed payload", characterId);
            return GenericActionResult.Aborted;
        }

        if (toll.Money is < 0 or > 100_000_000)
        {
            logger.LogInformation(
                "Character {CharacterId} teleport-toll aborted: amount {Money} out of range", characterId,
                toll.Money);
            return GenericActionResult.Aborted;
        }

        try
        {
            await characters.AdjustMoneyAsync(characterId, -toll.Money, 0, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} teleport-toll AdjustMoneyAsync failed (treated as insufficient balance)",
                characterId);
            return GenericActionResult.Aborted;
        }

        logger.LogInformation("Character {CharacterId} teleport-toll applied: money -{Money}", characterId,
            toll.Money);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> LearnSkillAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        if (!NpcSkillLearnData.TryRead(data, out var request))
        {
            logger.LogInformation("Character {CharacterId} skill-learn aborted: malformed payload", characterId);
            return GenericActionResult.Aborted;
        }

        var arrayKind = sort == 202 ? SkillLearnResolver.SkillTree1 : SkillLearnResolver.SkillTree2;
        var functionId = sort == 202 ? NpcFunctionGate.LearnSkillTree1 : NpcFunctionGate.LearnSkillTree2;

        if (!worldData.NpcsById.TryGetValue(request.NpcId, out var npc) ||
            !worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition) ||
            !NpcFunctionGate.IsAvailableAtNpc(zoneDefinition, npc, functionId, state.PosX, state.PosY,
                state.PosZ))
        {
            logger.LogInformation(
                "Character {CharacterId} skill-learn aborted: NPC {NpcId} unavailable/out of range", characterId,
                request.NpcId);
            return GenericActionResult.Aborted;
        }

        worldData.SkillsById.TryGetValue(request.SkillId, out var skillDefinition);

        var result = SkillLearnResolver.ResolveLearn(npc.SkillOffers, arrayKind, request.SkillId, skillDefinition,
            state.LearnedSkills, state.SkillPoints);

        if (!result.Success)
        {
            logger.LogInformation(
                "Character {CharacterId} skill-learn aborted by resolver (skill {SkillId}, skillPoints {SkillPoints})",
                characterId, request.SkillId, state.SkillPoints);
            return GenericActionResult.Aborted;
        }

        var learned = new LearnedSkill(request.SkillId, result.Cost);
        var newSkillPoints = state.SkillPoints - result.Cost;

        var actorResult = await zone.PostSkillCommandAndWaitForResultAsync(
            new SkillZoneCommand(characterId, result.Slot, learned, newSkillPoints), cancellationToken);
        if (actorResult.Kind != ZoneCommandResultKind.Applied)
        {
            logger.LogWarning(
                "Character {CharacterId} skill-learn rejected by zone actor: skill {SkillId}, outcome {Outcome}",
                characterId, request.SkillId, actorResult.Kind);
            return GenericActionResult.Aborted;
        }

        await characters.UpsertSkillSlotAsync(characterId, result.Slot, request.SkillId, result.Cost,
            cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} skill-learn applied: skill {SkillId} into slot {Slot}, cost {Cost}, skillPoints now {NewSkillPoints}",
            characterId, request.SkillId, result.Slot, result.Cost, newSkillPoints);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> UpgradeSkillAsync(byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken)
    {
        if (!SkillUpgradeData.TryRead(data, out var request))
        {
            logger.LogInformation("Character {CharacterId} skill-upgrade aborted: malformed payload", characterId);
            return GenericActionResult.Aborted;
        }

        var learned = default(LearnedSkill);
        SkillDefinition? skillDefinition = null;
        if (request.SkillIndex is >= 0 and < SkillLearnResolver.MaxSlots &&
            state.LearnedSkills.TryGetValue((byte)request.SkillIndex, out learned))
            worldData.SkillsById.TryGetValue(learned.SkillId, out skillDefinition);

        var result = SkillLearnResolver.ResolveUpgrade(request.SkillIndex, state.LearnedSkills, skillDefinition,
            state.SkillPoints);

        if (!result.Success)
        {
            logger.LogInformation(
                "Character {CharacterId} skill-upgrade aborted by resolver (slot {SkillIndex}, skillPoints {SkillPoints})",
                characterId, request.SkillIndex, state.SkillPoints);
            return GenericActionResult.Aborted;
        }

        var slot = (byte)request.SkillIndex;
        var upgraded = new LearnedSkill(learned.SkillId, result.NewGrade);
        var newSkillPoints = state.SkillPoints - 1;

        var actorResult = await zone.PostSkillCommandAndWaitForResultAsync(
            new SkillZoneCommand(characterId, slot, upgraded, newSkillPoints), cancellationToken);
        if (actorResult.Kind != ZoneCommandResultKind.Applied)
        {
            logger.LogWarning(
                "Character {CharacterId} skill-upgrade rejected by zone actor: slot {Slot}, outcome {Outcome}",
                characterId, slot, actorResult.Kind);
            return GenericActionResult.Aborted;
        }

        await characters.UpsertSkillSlotAsync(characterId, slot, learned.SkillId, result.NewGrade,
            cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} skill-upgrade applied: skill {SkillId} slot {Slot} -> grade {NewGrade}, skillPoints now {NewSkillPoints}",
            characterId, learned.SkillId, slot, result.NewGrade, newSkillPoints);

        return GenericActionResult.Succeeded;
    }

    public ValueTask<GenericActionResult> SellToNpcShopAsync(Zone zone, PlayerRuntimeState state,
        int accountId, int characterId, DefaultPData move, CancellationToken cancellationToken)
    {
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId))
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-sell disconnected: zone {MapId} is not a valid town server",
                characterId, zone.MapId);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        if (!worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition) ||
            !NpcFunctionGate.IsAvailable(zoneDefinition, worldData, NpcFunctionGate.NpcShop, state.PosX, state.PosY,
                state.PosZ))
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-sell aborted: shop out of range (zone {MapId})", characterId,
                zone.MapId);
            return ValueTask.FromResult(GenericActionResult.Failed);
        }

        var page1 = move.Page1;
        var index1 = move.Index1;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1))
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-sell disconnected: invalid slot ({Page1}:{Index1})", characterId,
                page1, index1);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        if (page1 == ContainerMatrix.InventoryPage1 && state.InventoryDate < GameDate.Today())
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-sell disconnected: dated-vault last page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        var sourceStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (sourceStack is not { } source || !worldData.ItemsById.TryGetValue(source.ItemId, out var itemDefinition))
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-sell disconnected: source slot empty/unresolvable", characterId);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        var resolved = NpcShopPolicy.ResolveSell(itemDefinition, source, move.Quantity1);
        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-sell disconnected by resolver (item {ItemId} x{Quantity})",
                characterId, source.ItemId, move.Quantity1);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        if (!HasAuthoritativeNonNegativeSellAmount(itemDefinition, move.Quantity1, resolved.MoneyGained) ||
            state.Money is < 0 or > MaximumMoneyBalance)
        {
            logger.LogWarning(
                "Character {CharacterId} NPC-shop-sell rejected: invalid authoritative amount or in-memory money balance (item {ItemId})",
                characterId, source.ItemId);
            return ValueTask.FromResult(GenericActionResult.Failed);
        }

        logger.LogWarning(
            "Character {CharacterId} NPC-shop-sell backpressured: ICharacterRepository lacks an idempotent money-and-container transaction (item {ItemId})",
            characterId, source.ItemId);
        return ValueTask.FromResult(GenericActionResult.Failed);
    }

    public async ValueTask<GenericActionResult> BuyFromNpcShopAsync(Zone zone, PlayerRuntimeState state,
        int accountId, int characterId, DefaultPData move, CancellationToken cancellationToken)
    {
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId))
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-buy disconnected: zone {MapId} is not a valid town server",
                characterId, zone.MapId);
            return GenericActionResult.Aborted;
        }

        if (!worldData.NpcsById.TryGetValue(move.Page1, out var npc))
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-buy disconnected: NPC {NpcId} not found", characterId,
                move.Page1);
            return GenericActionResult.Aborted;
        }

        if (!worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition) ||
            !NpcFunctionGate.IsAvailableAtNpc(zoneDefinition, npc, NpcFunctionGate.NpcShop, state.PosX, state.PosY,
                state.PosZ))
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-buy aborted: requested shop NPC {NpcId} is unavailable (zone {MapId})",
                characterId, move.Page1, zone.MapId);
            return GenericActionResult.Failed;
        }

        if (!worldData.ItemsById.TryGetValue(move.Index1, out var itemDefinition))
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-buy disconnected: item {ItemId} not found", characterId,
                move.Index1);
            return GenericActionResult.Aborted;
        }

        var page2 = move.Page2;
        var index2 = move.Index2;
        if (page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page2, index2) ||
            move.XPost2 is < 0 or > 7 || move.YPost2 is < 0 or > 7)
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-buy disconnected: invalid destination slot ({Page2}:{Index2})",
                characterId, page2, index2);
            return GenericActionResult.Aborted;
        }

        if (page2 == ContainerMatrix.InventoryPage1 && state.InventoryDate < GameDate.Today())
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-buy disconnected: dated-vault last page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return GenericActionResult.Aborted;
        }

        var destinationSlot = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (warPointShop is not null)
        {
            var warPointResult = await warPointShop.TryBuyAsync(zone, state, accountId, characterId, move.Page1,
                move.Index1, move.Quantity1, (byte)page2, (byte)index2, (byte)move.XPost2, (byte)move.YPost2,
                cancellationToken);

            switch (warPointResult.Status)
            {
                case WarPointBuyStatus.Aborted:
                    logger.LogInformation(
                        "Character {CharacterId} NPC-shop-buy rejected by War-Point routing ({Status}, NPC {NpcId}, item {ItemId})",
                        characterId, warPointResult.Status, move.Page1, move.Index1);
                    return GenericActionResult.Failed;

                case WarPointBuyStatus.SoftRejected:
                    logger.LogInformation(
                        "Character {CharacterId} NPC-shop-buy backpressured by War-Point routing (NPC {NpcId}, item {ItemId})",
                        characterId, move.Page1, move.Index1);
                    return GenericActionResult.Failed;

                case WarPointBuyStatus.Succeeded:
                    return GenericActionResult.Succeeded;

                case WarPointBuyStatus.NotHandled:
                    break;
            }
        }

        var resolved = NpcShopPolicy.ResolveBuy(npc, itemDefinition, move.Quantity1, destinationSlot, move.XPost2,
            move.YPost2, state.Level, state.ContributionPoints);

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} NPC-shop-buy disconnected by resolver ({Outcome}, item {ItemId} x{Quantity})",
                characterId, resolved.Outcome, move.Index1, move.Quantity1);
            return GenericActionResult.Aborted;
        }

        if (!HasAuthoritativeNonNegativeBuyAmounts(itemDefinition, move.Quantity1, resolved) ||
            state.Money is < 0 or > MaximumMoneyBalance || state.ContributionPoints < 0)
        {
            logger.LogWarning(
                "Character {CharacterId} NPC-shop-buy rejected: invalid authoritative amount or in-memory balance (item {ItemId})",
                characterId, itemDefinition.Item.ItemId);
            return GenericActionResult.Failed;
        }

        logger.LogWarning(
            "Character {CharacterId} NPC-shop-buy backpressured: ICharacterRepository lacks an idempotent money-and-container transaction (item {ItemId})",
            characterId, itemDefinition.Item.ItemId);
        return GenericActionResult.Failed;
    }

    public async ValueTask<GenericActionResult> TransferStoreItemAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} Store-item-transfer aborted: malformed payload (sort {Sort})", characterId,
                sort);
            return GenericActionResult.Aborted;
        }

        var secondInventoryPageAccessible = state.InventoryDate >= GameDate.Today();
        var secondStorePageAccessible = state.StoreDate >= GameDate.Today();

        byte fromContainer;
        byte toContainer;
        StoreItemTransferPolicy.TransferResult resolved;
        short auditEventCode = 0;

        switch (sort)
        {
            case 223 or 250:
            {
                if (!StoreItemTransferPolicy.TryResolveInventoryContainer(move.Page1, out fromContainer) ||
                    !StoreItemTransferPolicy.TryResolveStoreContainer(move.Page2, out toContainer))
                {
                    logger.LogDebug(
                        "Character {CharacterId} Store-item-transfer aborted: unresolvable containers (sort {Sort})",
                        characterId, sort);
                    return GenericActionResult.Aborted;
                }

                var (source, sourceIsStackable, sourceSupportsSocket) =
                    ResolveTransferSource(GetSlotOrNull(state, fromContainer, move.Index1));
                var destination = GetSlotOrNull(state, toContainer, move.Index2);

                resolved = StoreItemTransferPolicy.ResolveDepositFromInventory(fromContainer, move.Index1,
                    move.Quantity1, toContainer, move.Index2, source, destination, sourceIsStackable,
                    sourceSupportsSocket, secondInventoryPageAccessible, secondStorePageAccessible);
                auditEventCode = VaultTransferDepositEventCode;
                break;
            }
            case 224 or 248:
            {
                if (!StoreItemTransferPolicy.TryResolveStoreContainer(move.Page1, out fromContainer) ||
                    !StoreItemTransferPolicy.TryResolveInventoryContainer(move.Page2, out toContainer))
                {
                    logger.LogDebug(
                        "Character {CharacterId} Store-item-transfer aborted: unresolvable containers (sort {Sort})",
                        characterId, sort);
                    return GenericActionResult.Aborted;
                }

                var (source, sourceIsStackable, sourceSupportsSocket) =
                    ResolveTransferSource(GetSlotOrNull(state, fromContainer, move.Index1));
                var destination = GetSlotOrNull(state, toContainer, move.Index2);

                resolved = StoreItemTransferPolicy.ResolveWithdrawToInventory(fromContainer, move.Index1,
                    move.Quantity1, toContainer, move.Index2, move.XPost2, move.YPost2, source, destination,
                    sourceIsStackable, sourceSupportsSocket, secondStorePageAccessible,
                    secondInventoryPageAccessible);
                auditEventCode = VaultTransferWithdrawEventCode;
                break;
            }
            case 225:
            {
                if (!StoreItemTransferPolicy.TryResolveStoreContainer(move.Page1, out fromContainer) ||
                    !StoreItemTransferPolicy.TryResolveStoreContainer(move.Page2, out toContainer))
                {
                    logger.LogDebug(
                        "Character {CharacterId} Store-item-transfer aborted: unresolvable containers (sort {Sort})",
                        characterId, sort);
                    return GenericActionResult.Aborted;
                }

                var (source, sourceIsStackable, sourceSupportsSocket) =
                    ResolveTransferSource(GetSlotOrNull(state, fromContainer, move.Index1));
                var destination = GetSlotOrNull(state, toContainer, move.Index2);

                resolved = StoreItemTransferPolicy.ResolveRearrangeWithinStore(fromContainer, move.Index1,
                    move.Quantity1, toContainer, move.Index2, source, destination, sourceIsStackable,
                    sourceSupportsSocket, secondStorePageAccessible);
                break;
            }
            default:
                return GenericActionResult.Failed;
        }

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} Store-item-transfer aborted by policy: {Outcome} (sort {Sort})",
                characterId, resolved.Outcome, sort);
            return GenericActionResult.Aborted;
        }

        if (resolved.Outcome == StoreItemTransferPolicy.TransferOutcome.NoOp)
            return GenericActionResult.Succeeded;

        var fromCurrent = state.Inventory.GetContainer(fromContainer);
        var toCurrent = fromContainer == toContainer ? fromCurrent : state.Inventory.GetContainer(toContainer);

        ImmutableDictionary<byte, ItemStack> newFrom;
        ImmutableDictionary<byte, ItemStack> newTo;
        if (fromContainer == toContainer)
        {
            var updated = ApplySlotChange(fromCurrent, (byte)move.Index1, resolved.NewSource);
            updated = ApplySlotChange(updated, (byte)move.Index2, resolved.NewDestination);
            newFrom = updated;
            newTo = updated;
        }
        else
        {
            newFrom = ApplySlotChange(fromCurrent, (byte)move.Index1, resolved.NewSource);
            newTo = ApplySlotChange(toCurrent, (byte)move.Index2, resolved.NewDestination);
        }

        if (fromContainer == toContainer)
            await characters.ReplaceContainerAsync(characterId, fromContainer, ToTvps(newTo), cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, fromContainer, ToTvps(newFrom), toContainer,
                ToTvps(newTo), cancellationToken);

        if (resolved.IsNonStackableTransfer)
            await eventLog.LogAsync(auditEventCode, EventLogCategory.StoreSlotItem, accountId, characterId,
                null, null, null, null, null, resolved.NewDestination?.ItemId ?? resolved.NewSource?.ItemId,
                1, VaultTransferOutcome, null, cancellationToken);

        var containers = fromContainer == toContainer
            ? ImmutableArray.Create(new InventoryContainerSnapshot(fromContainer, newTo))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot(fromContainer, newFrom),
                new InventoryContainerSnapshot(toContainer, newTo));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped Store-transfer mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} Store-item-transfer applied: sort {Sort}, {FromContainer}:{Index1} -> {ToContainer}:{Index2}",
            characterId, sort, fromContainer, move.Index1, toContainer, move.Index2);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> TransferStoreMoneyAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} Store-money transfer aborted: malformed payload (sort {Sort})",
                characterId, sort);
            return GenericActionResult.Aborted;
        }

        if (move.Quantity1 < 1)
        {
            logger.LogInformation(
                "Character {CharacterId} Store-money transfer aborted: non-positive amount {Quantity1}",
                characterId, move.Quantity1);
            return GenericActionResult.Aborted;
        }

        var isDeposit = sort == 226;
        var deltaMoney = isDeposit ? -(long)move.Quantity1 : move.Quantity1;
        var deltaStoreMoney = isDeposit ? move.Quantity1 : -(long)move.Quantity1;
        var storeMoneyEventCode = isDeposit ? VaultTransferDepositEventCode : VaultTransferWithdrawEventCode;

        try
        {
            await characters.AdjustStoreMoneyAsync(characterId, deltaMoney, deltaStoreMoney, cancellationToken,
                accountId, storeMoneyEventCode, move.Quantity1);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} Store-money transfer AdjustStoreMoneyAsync failed (treated as insufficient balance/cap breach)",
                characterId);
            return GenericActionResult.Aborted;
        }

        var newStoreMoney = state.StoreMoney + deltaStoreMoney;
        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, StoreMoney: newStoreMoney), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Store-money mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} Store-money transfer applied: isDeposit={IsDeposit}, amount {Quantity1}, StoreMoney now {NewStoreMoney}",
            characterId, isDeposit, move.Quantity1, newStoreMoney);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> TransferBankItemAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} Save-item-transfer aborted: malformed payload (sort {Sort})", characterId,
                sort);
            return GenericActionResult.Aborted;
        }

        var (vaultBalance, vaultRows) = await accountVault.GetAsync(accountId, cancellationToken);
        var expectedVaultRevision = vaultBalance?.Revision ?? 0;
        var vaultBySlot = new Dictionary<short, ItemStack>(vaultRows.Count);
        foreach (var row in vaultRows)
            if (row.ItemId is not null)
                vaultBySlot[row.SlotIndex] = ItemStack.FromVaultRowV2(row);

        var secondInventoryPageAccessible = state.InventoryDate >= GameDate.Today();

        switch (sort)
        {
            case 228 or 251:
            {
                if (move.Page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1))
                {
                    logger.LogInformation(
                        "Character {CharacterId} Save-item-transfer aborted: invalid inventory page {Page1} (sort {Sort})",
                        characterId, move.Page1, sort);
                    return GenericActionResult.Aborted;
                }

                var inventoryContainer = (byte)move.Page1;
                var (source, sourceIsStackable, sourceSupportsSocket) =
                    ResolveTransferSource(GetSlotOrNull(state, inventoryContainer, move.Index1));
                var destination = GetVaultSlotOrNull(vaultBySlot, move.Index2);

                var resolved = SaveBankItemTransferPolicy.ResolveDepositFromInventory(inventoryContainer,
                    move.Index1, move.Quantity1, move.Index2, source, destination, sourceIsStackable,
                    sourceSupportsSocket, secondInventoryPageAccessible);

                if (!resolved.Succeeded)
                {
                    logger.LogInformation(
                        "Character {CharacterId} Save-item-transfer rejected by policy (deposit, sort {Sort})",
                        characterId, sort);
                    return GenericActionResult.Aborted;
                }

                if (resolved.Outcome == SaveBankItemTransferPolicy.TransferOutcome.NoOp)
                    return GenericActionResult.Succeeded;

                var newInventoryContainer = ApplySlotChange(state.Inventory.GetContainer(inventoryContainer),
                    (byte)move.Index1, resolved.NewSource);
                ApplyVaultSlotChange(vaultBySlot, (short)move.Index2, resolved.NewDestination);

                var applied = await accountVault.TryTransferItemWithCharacterAsync(accountId, characterId,
                    inventoryContainer, expectedVaultRevision,
                    new AccountVaultCharacterSlotMutation((byte)move.Index1,
                        ToCharacterItemSnapshot(source), ToCharacterItemSnapshot(resolved.NewSource)),
                    new AccountVaultItemSlotMutation((short)move.Index2, ToVaultItemSnapshot(destination),
                        ToVaultItemSnapshot(resolved.NewDestination)), cancellationToken);

                if (!applied)
                {
                    logger.LogInformation(
                        "Character {CharacterId} Save-item-transfer aborted: stale deposit precondition",
                        characterId);
                    return GenericActionResult.Aborted;
                }

                if (resolved.IsNonStackableTransfer)
                    await eventLog.LogAsync(VaultTransferDepositEventCode, EventLogCategory.SaveSlotItem,
                        accountId, characterId, null, null, null, null, null, resolved.NewDestination?.ItemId,
                        1, VaultTransferOutcome, null, cancellationToken);

                await MirrorInventoryContainerAsync(zone, characterId, inventoryContainer, newInventoryContainer,
                    cancellationToken);
                logger.LogInformation(
                    "Character {CharacterId} Save-item-transfer applied: deposit, inventory {InventoryContainer}:{Index1} -> vault slot {Index2}",
                    characterId, inventoryContainer, move.Index1, move.Index2);
                return GenericActionResult.Succeeded;
            }
            case 229 or 249:
            {
                if (move.Page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1))
                {
                    logger.LogInformation(
                        "Character {CharacterId} Save-item-transfer aborted: invalid inventory page {Page2} (sort {Sort})",
                        characterId, move.Page2, sort);
                    return GenericActionResult.Aborted;
                }

                var inventoryContainer = (byte)move.Page2;
                var (source, sourceIsStackable, sourceSupportsSocket) =
                    ResolveTransferSource(GetVaultSlotOrNull(vaultBySlot, move.Index1));
                var destination = GetSlotOrNull(state, inventoryContainer, move.Index2);

                var resolved = SaveBankItemTransferPolicy.ResolveWithdrawToInventory(move.Index1, move.Quantity1,
                    inventoryContainer, move.Index2, move.XPost2, move.YPost2, source, destination,
                    sourceIsStackable, sourceSupportsSocket, secondInventoryPageAccessible);

                if (!resolved.Succeeded)
                {
                    logger.LogInformation(
                        "Character {CharacterId} Save-item-transfer rejected by policy (withdraw, sort {Sort})",
                        characterId, sort);
                    return GenericActionResult.Aborted;
                }

                if (resolved.Outcome == SaveBankItemTransferPolicy.TransferOutcome.NoOp)
                    return GenericActionResult.Succeeded;

                var newInventoryContainer = ApplySlotChange(state.Inventory.GetContainer(inventoryContainer),
                    (byte)move.Index2, resolved.NewDestination);
                ApplyVaultSlotChange(vaultBySlot, (short)move.Index1, resolved.NewSource);

                var applied = await accountVault.TryTransferItemWithCharacterAsync(accountId, characterId,
                    inventoryContainer, expectedVaultRevision,
                    new AccountVaultCharacterSlotMutation((byte)move.Index2,
                        ToCharacterItemSnapshot(destination), ToCharacterItemSnapshot(resolved.NewDestination)),
                    new AccountVaultItemSlotMutation((short)move.Index1, ToVaultItemSnapshot(source),
                        ToVaultItemSnapshot(resolved.NewSource)), cancellationToken);

                if (!applied)
                {
                    logger.LogInformation(
                        "Character {CharacterId} Save-item-transfer aborted: stale withdrawal precondition",
                        characterId);
                    return GenericActionResult.Aborted;
                }

                if (resolved.IsNonStackableTransfer)
                    await eventLog.LogAsync(VaultTransferWithdrawEventCode, EventLogCategory.SaveSlotItem,
                        accountId, characterId, null, null, null, null, null, resolved.NewDestination?.ItemId,
                        1, VaultTransferOutcome, null, cancellationToken);

                await MirrorInventoryContainerAsync(zone, characterId, inventoryContainer, newInventoryContainer,
                    cancellationToken);
                logger.LogInformation(
                    "Character {CharacterId} Save-item-transfer applied: withdraw, vault slot {Index1} -> inventory {InventoryContainer}:{Index2}",
                    characterId, move.Index1, inventoryContainer, move.Index2);
                return GenericActionResult.Succeeded;
            }
            case 230:
            {
                var (source, sourceIsStackable, sourceSupportsSocket) =
                    ResolveTransferSource(GetVaultSlotOrNull(vaultBySlot, move.Index1));
                var destination = GetVaultSlotOrNull(vaultBySlot, move.Index2);

                var resolved = SaveBankItemTransferPolicy.ResolveRearrangeWithinBank(move.Index1, move.Quantity1,
                    move.Index2, source, destination, sourceIsStackable, sourceSupportsSocket);

                if (!resolved.Succeeded)
                {
                    logger.LogInformation(
                        "Character {CharacterId} Save-item-transfer rejected by policy (rearrange)", characterId);
                    return GenericActionResult.Aborted;
                }

                if (resolved.Outcome == SaveBankItemTransferPolicy.TransferOutcome.NoOp)
                    return GenericActionResult.Succeeded;

                ApplyVaultSlotChange(vaultBySlot, (short)move.Index1, resolved.NewSource);
                ApplyVaultSlotChange(vaultBySlot, (short)move.Index2, resolved.NewDestination);

                var applied = await accountVault.TryRearrangeItemsAsync(accountId, expectedVaultRevision,
                    new AccountVaultItemSlotMutation((short)move.Index1, ToVaultItemSnapshot(source),
                        ToVaultItemSnapshot(resolved.NewSource)),
                    new AccountVaultItemSlotMutation((short)move.Index2, ToVaultItemSnapshot(destination),
                        ToVaultItemSnapshot(resolved.NewDestination)), cancellationToken);

                if (!applied)
                {
                    logger.LogInformation(
                        "Character {CharacterId} Save-item-transfer aborted: stale rearrange precondition",
                        characterId);
                    return GenericActionResult.Aborted;
                }
                logger.LogInformation(
                    "Character {CharacterId} Save-item-transfer applied: rearrange, vault {Index1} <-> {Index2}",
                    characterId, move.Index1, move.Index2);
                return GenericActionResult.Succeeded;
            }
            default:
                return GenericActionResult.Aborted;
        }
    }

    public async ValueTask<GenericActionResult> TransferBankMoneyAsync(int sort, byte[] data, int accountId,
        int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} Save-money transfer aborted: malformed payload (sort {Sort})",
                characterId, sort);
            return GenericActionResult.Aborted;
        }

        if (move.Quantity1 < 1)
        {
            logger.LogInformation(
                "Character {CharacterId} Save-money transfer aborted: non-positive amount {Quantity1}",
                characterId, move.Quantity1);
            return GenericActionResult.Aborted;
        }

        var isDeposit = sort == 231;
        var deltaCharacterMoney = isDeposit ? -(long)move.Quantity1 : move.Quantity1;
        var deltaVaultMoney = isDeposit ? move.Quantity1 : -(long)move.Quantity1;
        var saveMoneyEventCode = isDeposit ? VaultTransferDepositEventCode : VaultTransferWithdrawEventCode;

        try
        {
            await accountVault.TransferMoneyWithCharacterAsync(characterId, deltaCharacterMoney, accountId,
                deltaVaultMoney, cancellationToken, saveMoneyEventCode,
                move.Quantity1);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} account-vault money transfer failed (treated as insufficient balance/cap breach)",
                characterId);
            return GenericActionResult.Aborted;
        }

        logger.LogInformation(
            "Character {CharacterId} Save-money transfer applied: isDeposit={IsDeposit}, amount {Quantity1}",
            characterId, isDeposit, move.Quantity1);

        return GenericActionResult.Succeeded;
    }

    public ValueTask<GenericActionResult> TransferTradeItemAsync(int sort, byte[] data, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} Trade-item-transfer aborted: malformed payload (sort {Sort})", characterId,
                sort);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        if (!trades.TryGetSession(characterId, out var trade) || trade is null)
        {
            logger.LogInformation(
                "Character {CharacterId} Trade-item-transfer aborted: no active trade session (sort {Sort})",
                characterId, sort);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        var side = trade.SideOf(characterId);
        if (side.MenuState >= TradeBigMoneyPlacementResolver.LockedMenuState)
        {
            logger.LogInformation(
                "Character {CharacterId} Trade-item-transfer aborted: own offer already locked (sort {Sort})",
                characterId, sort);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        var secondInventoryPageAccessible = state.InventoryDate >= GameDate.Today();

        var applied = sort switch
        {
            218 => TryTradeDeposit(move, state, side, secondInventoryPageAccessible),
            219 => TryTradeWithdraw(move, state, side, secondInventoryPageAccessible),
            220 => TryTradeRearrange(move, side),
            _ => false
        };

        if (!applied)
        {
            logger.LogInformation(
                "Character {CharacterId} Trade-item-transfer aborted by policy (sort {Sort})", characterId, sort);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        TradeOfferResyncNotifier.TryNotifyOpponent(trades, zoneRegistry, characterId);

        logger.LogInformation("Character {CharacterId} Trade-item-transfer applied (sort {Sort})", characterId,
            sort);
        return ValueTask.FromResult(GenericActionResult.Succeeded);
    }

    public ValueTask<GenericActionResult> TransferTradeMoneyAsync(int sort, byte[] data, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} Trade-money-transfer aborted: malformed payload (sort {Sort})",
                characterId, sort);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        if (!trades.TryGetSession(characterId, out var trade) || trade is null)
        {
            logger.LogInformation(
                "Character {CharacterId} Trade-money-transfer aborted: no active trade session (sort {Sort})",
                characterId, sort);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        var side = trade.SideOf(characterId);
        if (side.MenuState >= TradeBigMoneyPlacementResolver.LockedMenuState)
        {
            logger.LogInformation(
                "Character {CharacterId} Trade-money-transfer aborted: own offer already locked (sort {Sort})",
                characterId, sort);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        var resolved = sort == 221
            ? TradeMoneyPlacementResolver.ResolveToTradeOffer(long.MaxValue, side.Money, move.Quantity1)
            : TradeMoneyPlacementResolver.ResolveFromTradeOffer(side.Money, 0, move.Quantity1);

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} Trade-money-transfer aborted by policy (sort {Sort})", characterId, sort);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        side.Money = resolved.NewTradeOfferMoney;

        TradeOfferResyncNotifier.TryNotifyOpponent(trades, zoneRegistry, characterId);

        logger.LogInformation("Character {CharacterId} Trade-money-transfer applied (sort {Sort})", characterId,
            sort);
        return ValueTask.FromResult(GenericActionResult.Succeeded);
    }

    public async ValueTask<GenericActionResult> AllocateStatPointAsync(int statSort, int addValue, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var resolved = StatAllocationResolver.Resolve(statSort, addValue, state.StatPoints);
        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} stat-point allocation aborted: illegal category or unaffordable amount (statSort {StatSort}, addValue {AddValue}, available {StatPoints})",
                characterId, statSort, addValue, state.StatPoints);
            return GenericActionResult.Aborted;
        }

        var newVit = state.StatVit + (resolved.Stat == StatAllocationResolver.BaseStat.Vitality ? resolved.Amount : 0);
        var newStr = state.StatStr + (resolved.Stat == StatAllocationResolver.BaseStat.Strength ? resolved.Amount : 0);
        var newInt = state.StatInt +
                     (resolved.Stat == StatAllocationResolver.BaseStat.Intelligence ? resolved.Amount : 0);
        var newDex = state.StatDex + (resolved.Stat == StatAllocationResolver.BaseStat.Dexterity ? resolved.Amount : 0);

        var attributes = new CharacterBaseAttributes(newVit, newStr, newInt, newDex, state.Level, state.Tribe,
            state.PreviousTribe, state.Title, state.Halo, state.RebirthCount, state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);

        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, state);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                StatVit: newVit, StatStr: newStr, StatInt: newInt, StatDex: newDex,
                StatPoints: resolved.NewStatPoints, MaxLife: updatedStats.MaxLife, MaxMana: updatedStats.MaxMana,
                UpdatedStats: updatedStats), cancellationToken) ||
            !zone.TryGetPlayer(characterId, out var mirroredState) || !ReferenceEquals(mirroredState, state))
        {
            logger.LogError(
                "Zone {MapId} could not confirm the stat-allocation mirror for character {CharacterId}",
                zone.MapId, characterId);
            return GenericActionResult.Failed;
        }

        logger.LogInformation(
            "Character {CharacterId} stat-point allocation applied: {Stat} +{Amount}, statPoints now {NewStatPoints}",
            characterId, resolved.Stat, resolved.Amount, resolved.NewStatPoints);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> TimeExchangeAsync(Zone zone, PlayerRuntimeState state,
        int accountId, int characterId, CancellationToken cancellationToken)
    {
        var accruedMinutes = state.PlayTimeEvent;
        if (accruedMinutes < 1)
        {
            logger.LogDebug("Character {CharacterId} TimeExchange no-op: no accrued play-time-event minutes",
                characterId);
            return GenericActionResult.Succeeded;
        }

        var teacherPointsGranted = accruedMinutes * TeacherPointsPerPlayTimeMinute;
        var petExperienceGranted = accruedMinutes * PetExperiencePerPlayTimeMinute;

        var petItemId = state.Inventory.GetContainer(ContainerMatrix.Equipment)
            .TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var preGrantPetActivity = state.PetActivity;
        var preGrantPetGrowth = state.PetGrowth;

        var credited = PetExperienceCreditResolver.Resolve(petItemId, state.PetGrowth, state.PetActivity,
            petExperienceGranted, worldData.ItemsById);

        await eventLog.LogAsync(TimeExchangeEventCode, EventLogCategory.PlayTimeExchange, accountId, characterId,
            null, null, null, null, null, petItemId, teacherPointsGranted, TimeExchangeOutcome,
            $"PetActivity={preGrantPetActivity};PetGrowth={preGrantPetGrowth};PetExperienceGranted={petExperienceGranted}",
            cancellationToken);

        var newTeacherPoint = state.TeacherPoint + teacherPointsGranted;

        int? petGrowthToApply = null;
        byte? petActivityToApply = null;
        if (credited.IsEligible && (credited.CreditedAmount > 0 || credited.ReactivationApplied))
        {
            petGrowthToApply = credited.NewGrowth;
            petActivityToApply = (byte)credited.NewActivity;
        }

        if (!await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                TeacherPoint: newTeacherPoint,
                PetGrowth: petGrowthToApply,
                PetActivity: petActivityToApply,
                PlayTimeEvent: 0), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped TimeExchange mirror for character {CharacterId}",
                zone.MapId, characterId);

        var grantedPetGrowth = credited is { IsEligible: true, CreditedAmount: > 0 } ? credited.NewGrowth : (int?)null;

        logger.LogInformation(
            "Character {CharacterId} TimeExchange applied: {AccruedMinutes} minute(s) -> {TeacherPointsGranted} teacher points, pet growth {GrantedPetGrowth}",
            characterId, accruedMinutes, teacherPointsGranted, grantedPetGrowth);

        return new GenericActionResult(GenericActionStatus.Succeeded, GrantedPetExperienceGrowth: grantedPetGrowth);
    }

    public async ValueTask<GenericActionResult> ExchangeMeritForContributionPointsAsync(int requestedUnits,
        Zone zone, PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var outcome = MeritContributionPointExchangeResolver.Evaluate(requestedUnits, state.Level,
            state.TeacherPoint);

        if (outcome != MeritContributionPointExchangeOutcome.Success)
        {
            logger.LogInformation(
                "Character {CharacterId} Merit-to-CP exchange aborted: {Outcome} (requestedUnits {RequestedUnits}, level {Level}, teacherPoint {TeacherPoint})",
                characterId, outcome, requestedUnits, state.Level, state.TeacherPoint);
            return GenericActionResult.Failed;
        }

        var teacherPointCost = requestedUnits * MeritContributionPointExchangeResolver.TeacherPointCostPerUnit;
        var newTeacherPoint = state.TeacherPoint - teacherPointCost;
        var newContributionPoints = Math.Max(0, state.ContributionPoints + requestedUnits);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                TeacherPoint: newTeacherPoint, ContributionPoints: newContributionPoints), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Merit-to-CP exchange mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} Merit-to-CP exchange applied: -{TeacherPointCost} teacher points, +{RequestedUnits} contribution points",
            characterId, teacherPointCost, requestedUnits);

        return GenericActionResult.Succeeded;
    }

    private bool TryTradeDeposit(DefaultPData move, PlayerRuntimeState state, TradeOfferSide side,
        bool secondInventoryPageAccessible)
    {
        if (!TradeItemPlacementResolver.IsValidInventoryPage(move.Page1) ||
            !TradeItemPlacementResolver.IsValidInventorySlot(move.Index1) ||
            !TradeItemPlacementResolver.IsValidTradeSlot(move.Index2))
            return false;

        if (move.Page1 == ContainerMatrix.InventoryPage1 && !secondInventoryPageAccessible)
            return false;

        var originContainer = (byte)move.Page1;
        var originSlot = (byte)move.Index1;
        var destinationEntry = side.Slots[move.Index2];

        if (destinationEntry is { } occupant &&
            (occupant.Container != originContainer || occupant.Slot != originSlot))
            return false;

        var liveStack = state.Inventory.GetSlot(originContainer, originSlot);

        var itemDefinition = liveStack is { } ls && worldData.ItemsById.TryGetValue(ls.ItemId, out var def)
            ? def
            : null;

        var isStackable = itemDefinition is not null && ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        ItemStack? effectiveSource;
        if (isStackable)
        {
            var stagedElsewhere = side.GetOriginStagedQuantity(originContainer, originSlot, move.Index2);
            effectiveSource = ReduceByAlreadyStaged(liveStack, stagedElsewhere);
        }
        else
        {
            if (side.ReservesOrigin(originContainer, originSlot, move.Index2))
                return false;

            effectiveSource = liveStack;
        }

        var resolved = TradeItemPlacementResolver.ResolveDeposit(effectiveSource, move.Quantity1,
            destinationEntry?.Stack, itemDefinition, false);

        if (!resolved.Succeeded)
            return false;

        side.Slots[move.Index2] = resolved.NewDestination is { } newDestination
            ? (originContainer, originSlot, newDestination)
            : null;

        return true;
    }

    private bool TryTradeWithdraw(DefaultPData move, PlayerRuntimeState state, TradeOfferSide side,
        bool secondInventoryPageAccessible)
    {
        if (!TradeItemPlacementResolver.IsValidTradeSlot(move.Index1) ||
            !TradeItemPlacementResolver.IsValidInventoryPage(move.Page2) ||
            !TradeItemPlacementResolver.IsValidInventorySlot(move.Index2) ||
            !TradeItemPlacementResolver.IsValidGridCoordinate(move.XPost2) ||
            !TradeItemPlacementResolver.IsValidGridCoordinate(move.YPost2))
            return false;

        var destinationContainer = (byte)move.Page2;
        if (destinationContainer == ContainerMatrix.InventoryPage1 && !secondInventoryPageAccessible)
            return false;

        var sourceEntry = side.Slots[move.Index1];
        var sourceStack = sourceEntry?.Stack;
        var destinationSlot = (byte)move.Index2;
        var destinationStack = state.Inventory.GetSlot(destinationContainer, destinationSlot);

        var itemDefinition = sourceStack is { } ss && worldData.ItemsById.TryGetValue(ss.ItemId, out var def)
            ? def
            : null;

        var resolved = TradeItemPlacementResolver.ResolveWithdrawal(sourceStack, move.Quantity1, destinationStack,
            itemDefinition, false);

        if (!resolved.Succeeded)
            return false;

        side.Slots[move.Index1] = resolved.NewSource is { } remainder
            ? (sourceEntry!.Value.Container, sourceEntry.Value.Slot, remainder)
            : null;

        return true;
    }

    private bool TryTradeRearrange(DefaultPData move, TradeOfferSide side)
    {
        if (!TradeItemPlacementResolver.IsValidTradeSlot(move.Index1) ||
            !TradeItemPlacementResolver.IsValidTradeSlot(move.Index2))
            return false;

        if (move.Index1 == move.Index2)
            return true;

        var sourceEntry = side.Slots[move.Index1];
        var destinationEntry = side.Slots[move.Index2];

        if (sourceEntry is { } origin && destinationEntry is { } occupant &&
            (occupant.Container != origin.Container || occupant.Slot != origin.Slot))
            return false;

        var sourceStack = sourceEntry?.Stack;
        var itemDefinition = sourceStack is { } ss && worldData.ItemsById.TryGetValue(ss.ItemId, out var def)
            ? def
            : null;

        var resolved = TradeItemPlacementResolver.ResolveRearrange(sourceStack, move.Quantity1,
            destinationEntry?.Stack, itemDefinition, false);

        if (!resolved.Succeeded)
            return false;

        var sourceOrigin = sourceEntry!.Value;
        side.Slots[move.Index1] = resolved.NewSource is { } remainder
            ? (sourceOrigin.Container, sourceOrigin.Slot, remainder)
            : null;
        side.Slots[move.Index2] = resolved.NewDestination is { } newDestination
            ? (sourceOrigin.Container, sourceOrigin.Slot, newDestination)
            : null;

        return true;
    }

    private bool TouchesLiveTradeReservation(int characterId, byte fromContainer, int fromSlot, byte toContainer,
        int toSlot)
    {
        if (!trades.TryGetSession(characterId, out var trade) || trade is null)
            return false;

        var offerSide = trade.SideOf(characterId);
        return Reserves(offerSide, fromContainer, fromSlot) || Reserves(offerSide, toContainer, toSlot);

        static bool Reserves(TradeOfferSide side, byte container, int slot)
        {
            return ContainerMatrix.IsValidSlot(container, slot) && side.ReservesOrigin(container, (byte)slot);
        }
    }

    private static ItemStack? ReduceByAlreadyStaged(ItemStack? liveStack, long alreadyStagedQuantity)
    {
        if (liveStack is not { } stack)
            return null;

        var remaining = stack.Quantity - alreadyStagedQuantity;
        return remaining <= 0 ? null : stack with { Quantity = (int)remaining };
    }

    private static bool IsInventoryToInventoryRequestValid(DefaultPData move, int inventoryDate)
    {
        if (move.Page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)move.Page1, move.Index1))
            return false;

        if (move.Page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)move.Page2, move.Index2))
            return false;

        if (move.XPost2 is < 0 or > 7 || move.YPost2 is < 0 or > 7)
            return false;

        var today = GameDate.Today();
        return RentedInventoryPageGate.IsPageAccessible(move.Page1, inventoryDate, today) &&
               RentedInventoryPageGate.IsPageAccessible(move.Page2, inventoryDate, today);
    }

    private static bool MeetsGmTier(PlayerRuntimeState state, GmCommandTier tier)
    {
        return state.Session is IZoneSession zoneSession && zoneSession.MeetsGmTier(tier);
    }

    private async ValueTask<GenericActionResult> MusterZone124DuelReadyAsync(Zone zone, int characterId,
        CancellationToken cancellationToken)
    {
        var mustered = 0;

        foreach (var candidate in zone.Players)
        {
            var isDuelEngaged = duels is not null &&
                                (duels.IsNegotiating(candidate.CharacterId) ||
                                 duels.TryGetActiveDuel(candidate.CharacterId, out _));

            var placement = Zone124DuelReadyResolver.Place(candidate.IsMovingZone, isDuelEngaged, candidate.PosX,
                candidate.PosY, candidate.PosZ);

            if (placement.Side == Zone124DuelReadySide.None)
                continue;

            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(candidate.CharacterId,
                        TeleportTo: (placement.X, placement.Y, placement.Z),
                        FullActionRebroadcast: true, ResetAfkTick: true), cancellationToken))
            {
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped DUEL-READY muster for character {CharacterId}",
                    zone.MapId, candidate.CharacterId);
                continue;
            }

            mustered++;
        }

        logger.LogInformation(
            "Character {CharacterId} applied GM command {Command} (sort {Sort}) on map {MapId}: {MusteredCount} player(s) relocated to the line-up points",
            characterId, Zone124DuelReadyResolver.CommandName, Zone124DuelReadyResolver.Sort, zone.MapId, mustered);

        return GenericActionResult.Succeeded;
    }

    private async ValueTask<GenericActionResult> StartZone124DuelAsync(Zone zone, int characterId,
        CancellationToken cancellationToken)
    {
        var recruits = new List<(PlayerRuntimeState Player, Zone124DuelStartRecruitment Recruitment)>();
        var westCount = 0;
        var eastCount = 0;

        foreach (var candidate in zone.Players)
        {
            var isDuelEngaged = duels is not null &&
                                (duels.IsNegotiating(candidate.CharacterId) ||
                                 duels.TryGetActiveDuel(candidate.CharacterId, out _));

            var recruitment = Zone124DuelStartResolver.Recruit(candidate.IsMovingZone, isDuelEngaged, candidate.PosX,
                candidate.PosY, candidate.PosZ);

            switch (recruitment.Side)
            {
                case Zone124DuelStartSide.West:
                    westCount++;
                    break;

                case Zone124DuelStartSide.East:
                    eastCount++;
                    break;

                case Zone124DuelStartSide.None:
                default:
                    continue;
            }

            recruits.Add((candidate, recruitment));
        }

        if (!Zone124DuelStartResolver.HasBothCamps(westCount, eastCount))
        {
            logger.LogInformation(
                "Character {CharacterId} invoked GM command {Command} (sort {Sort}) on map {MapId} with {WestCount} west / {EastCount} east recruit(s): a camp is empty, refused without touching any player",
                characterId, Zone124DuelStartResolver.CommandName, Zone124DuelStartResolver.Sort, zone.MapId,
                westCount, eastCount);
            return GenericActionResult.Failed;
        }

        var sessionNumber = Zone124DuelStartResolver.AllocateSessionNumber(characterId, zone.RawLogicTick);
        var engaged = 0;

        foreach (var (player, recruitment) in recruits)
        {
            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(player.CharacterId,
                        TeleportTo: (recruitment.X, recruitment.Y, recruitment.Z),
                        FullActionRebroadcast: true, ResetAfkTick: true), cancellationToken))
            {
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped DUEL-START relocation for character {CharacterId}",
                    zone.MapId, player.CharacterId);
                continue;
            }

            player.Session.Send(new DuelStartResponse
            {
                DuelState = Zone124DuelStartResolver.BuildDuelState(recruitment.Side, sessionNumber),
                RemainTime = Zone124DuelStartResolver.DurationUnits,
                EatDrugState = Zone124DuelStartResolver.EatDrugState
            });

            engaged++;
        }

        logger.LogWarning(
            "Character {CharacterId} applied GM command {Command} (sort {Sort}) on map {MapId}: mass duel {SessionNumber} engaged {EngagedCount} player(s), {WestCount} west vs {EastCount} east",
            characterId, Zone124DuelStartResolver.CommandName, Zone124DuelStartResolver.Sort, zone.MapId,
            sessionNumber, engaged, westCount, eastCount);

        return GenericActionResult.Succeeded;
    }

    private GenericActionResult EndZone124Duel(Zone zone, int characterId)
    {
        zone.ResetZone124MassDuel();

        var engaged = new List<PlayerRuntimeState>();

        foreach (var candidate in zone.Players)
        {
            var isDuelEngaged = duels is not null && duels.TryGetActiveDuel(candidate.CharacterId, out _);

            if (Zone124DuelEndResolver.Clears(candidate.IsMovingZone, isDuelEngaged))
                engaged.Add(candidate);
        }

        foreach (var player in engaged)
        {
            duels!.TryEndActiveDuel(player.CharacterId, out _);

            player.CanUseConsumables = true;
            player.Session.Send(new DuelEndResponse { Result = Zone124DuelEndResolver.Result });

            BroadcastZone124DuelStateCleared(zone, player);
        }

        logger.LogWarning(
            "Character {CharacterId} applied GM command {Command} (sort {Sort}) on map {MapId}: mass duel torn down, {ClearedCount} player(s) released",
            characterId, Zone124DuelEndResolver.CommandName, Zone124DuelEndResolver.Sort, zone.MapId,
            engaged.Count);

        return GenericActionResult.Succeeded;
    }

    private void BroadcastZone124DuelStateCleared(Zone zone, PlayerRuntimeState subject)
    {
        var packet = new AvatarStateFlagResponse
        {
            ServerIndex = subject.CharacterId,
            UniqueNumber = subject.UniqueNumber,
            Sort = Zone124DuelEndResolver.ClearedDuelStateSort,
            Value01 = 0,
            Value02 = 0,
            Value03 = 0
        };

        var radius = zone.AoiCellSize * Zone124DuelEndResolver.BroadcastScale;

        foreach (var recipient in zone.Players)
        {
            if (recipient.IsMovingZone ||
                !Zone124DuelEndResolver.IsWithinBroadcastRadius(subject.PosX, subject.PosY, subject.PosZ,
                    recipient.PosX, recipient.PosY, recipient.PosZ, radius))
                continue;

            try
            {
                recipient.Session.Send(packet);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Zone {MapId} DUEL-END duel-state broadcast to character {RecipientId} failed", zone.MapId,
                    recipient.CharacterId);
            }
        }
    }

    private async ValueTask<GenericActionResult> EvacuateZone124DuelAsync(Zone zone, int characterId,
        CancellationToken cancellationToken)
    {
        zone.ResetZone124MassDuel();

        var evacuated = 0;

        foreach (var candidate in zone.Players)
        {
            if (!Zone124DuelOutResolver.IsInsideArena(candidate.IsMovingZone, candidate.PosX, candidate.PosY,
                    candidate.PosZ))
                continue;

            if (duels is not null)
                duels.TryEndActiveDuel(candidate.CharacterId, out _);

            candidate.CanUseConsumables = true;

            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(candidate.CharacterId,
                        TeleportTo: Zone124DuelOutResolver.EvacuationPoint,
                        FullActionRebroadcast: true, ResetAfkTick: true), cancellationToken))
            {
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped DUEL-OUT evacuation for character {CharacterId}",
                    zone.MapId, candidate.CharacterId);
                continue;
            }

            BroadcastZone124DuelStateCleared(zone, candidate);

            evacuated++;
        }

        logger.LogWarning(
            "Character {CharacterId} applied GM command {Command} (sort {Sort}) on map {MapId}: {EvacuatedCount} player(s) evacuated to the fallback point",
            characterId, Zone124DuelOutResolver.CommandName, Zone124DuelOutResolver.Sort, zone.MapId, evacuated);

        return GenericActionResult.Succeeded;
    }

    private static ItemStack? GetSlotOrNull(PlayerRuntimeState state, byte container, int slot)
    {
        return ContainerMatrix.IsValidSlot(container, slot) ? state.Inventory.GetSlot(container, (byte)slot) : null;
    }

    private static ItemStack? GetVaultSlotOrNull(IReadOnlyDictionary<short, ItemStack> vaultBySlot, int slot)
    {
        return SaveBankItemTransferPolicy.IsValidSlot(slot) && vaultBySlot.TryGetValue((short)slot, out var stack)
            ? stack
            : null;
    }

    private static void ApplyVaultSlotChange(Dictionary<short, ItemStack> vaultBySlot, short slot,
        ItemStack? newValue)
    {
        if (newValue is { } value)
            vaultBySlot[slot] = value;
        else
            vaultBySlot.Remove(slot);
    }

    private static AccountVaultCharacterItemSnapshot? ToCharacterItemSnapshot(ItemStack? stack)
    {
        return stack is not { } value
            ? null
            : new AccountVaultCharacterItemSnapshot(value.ItemId, value.Quantity, value.Enchant, value.Combine,
                value.Refine, value.Socket, value.SocketGem1, value.SocketGem2, value.SocketGem3,
                value.ExpireDate, value.Serial, value.XPos, value.YPos);
    }

    private static AccountVaultItemSnapshot? ToVaultItemSnapshot(ItemStack? stack)
    {
        return stack is not { } value
            ? null
            : new AccountVaultItemSnapshot(value.ItemId, value.Quantity,
                ItemValueCodec.Encode(value.Enchant, value.Combine, value.Refine, value.Socket), value.Serial, null,
                value.SocketGem1, value.SocketGem2, value.SocketGem3, value.ExpireDate);
    }

    private static ImmutableDictionary<byte, ItemStack> ApplySlotChange(
        ImmutableDictionary<byte, ItemStack> current, byte slot, ItemStack? newValue)
    {
        return newValue is { } value ? current.SetItem(slot, value) : current.Remove(slot);
    }

    private (ItemStack? Source, bool IsStackable, bool SupportsSocket) ResolveTransferSource(ItemStack? candidate)
    {
        if (candidate is not { } stack || !worldData.ItemsById.TryGetValue(stack.ItemId, out var definition))
            return (null, false, false);

        return (stack, ContainerMatrix.IsStackableSort(definition.Item.Sort),
            ContainerMatrix.IsSocketableItem(definition.Item.Sort, definition.Item.Type));
    }

    private static ItemStack FilterSocketState(ItemStack stack, byte itemSort, byte itemType)
    {
        if (ContainerMatrix.IsSocketableItem(itemSort, itemType))
            return stack;

        return stack with { Socket = 0, SocketGem1 = 0, SocketGem2 = 0, SocketGem3 = 0 };
    }

    private static bool HasAuthoritativeNonNegativeSellAmount(ItemDefinition itemDefinition, int requestedQuantity,
        long resolvedMoneyGained)
    {
        var effectiveQuantity = NpcShopPolicy.IsStackableSellSort(itemDefinition.Item.Sort) ? requestedQuantity : 1;
        if (effectiveQuantity < 1)
            return false;

        try
        {
            var expectedMoneyGained = checked((long)itemDefinition.Item.SellCost * effectiveQuantity);
            return expectedMoneyGained is >= 0 and <= MaximumMoneyBalance &&
                   resolvedMoneyGained == expectedMoneyGained;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool HasAuthoritativeNonNegativeBuyAmounts(ItemDefinition itemDefinition, int requestedQuantity,
        NpcShopPolicy.BuyResult resolution)
    {
        var effectiveQuantity = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort) ? requestedQuantity : 1;
        if (effectiveQuantity < 1)
            return false;

        try
        {
            var expectedMoneyCost = checked((long)itemDefinition.Item.BuyCost * effectiveQuantity);
            var expectedContributionPointCost = checked((long)itemDefinition.Item.BuyCost2 * effectiveQuantity);
            return expectedMoneyCost is >= 0 and <= MaximumMoneyBalance &&
                   expectedContributionPointCost is >= 0 and <= MaximumMoneyBalance &&
                   resolution.MoneyCost == expectedMoneyCost &&
                   resolution.CpCost == expectedContributionPointCost;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private async ValueTask MirrorInventoryContainerAsync(Zone zone, int characterId, byte container,
        ImmutableDictionary<byte, ItemStack> contents, CancellationToken cancellationToken)
    {
        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, contents));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped account-vault transfer mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
