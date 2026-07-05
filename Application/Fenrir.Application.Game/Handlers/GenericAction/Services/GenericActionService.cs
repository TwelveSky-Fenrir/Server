using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Pets;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tribes;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Application.Game.World.Npcs;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.Characters;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.GenericAction.Services;

/// <summary>Outcome discriminator for a <see cref="GenericActionService" /> operation.</summary>
public enum GenericActionStatus
{
    /// <summary>Malformed/impossible input -- the caller should abort the session (anti-fuzzing).</summary>
    Aborted,

    /// <summary>A well-formed request the domain rules cleanly rejected -- the caller replies with a failure code.</summary>
    Failed,

    /// <summary>The action was applied -- the caller replies with a success code.</summary>
    Succeeded
}

/// <summary>
///     Result of a <see cref="GenericActionService" /> operation. <see cref="NotifyQuestProgress" /> is only ever
///     set on a successful ground-item pickup that also happens to satisfy the character's active qSort-2 quest.
/// </summary>
public readonly record struct GenericActionResult(GenericActionStatus Status, bool NotifyQuestProgress = false)
{
    public static readonly GenericActionResult Aborted = new(GenericActionStatus.Aborted);
    public static readonly GenericActionResult Failed = new(GenericActionStatus.Failed);
    public static readonly GenericActionResult Succeeded = new(GenericActionStatus.Succeeded);
}

/// <summary>
///     Business logic behind <see cref="GenericActionHandler" />'s tSort-dispatched actions: container moves,
///     ground pickup, NPC teleport toll, skill learn/upgrade, and NPC shop buy/sell.
/// </summary>
public interface IGenericActionService
{
    ValueTask<GenericActionResult> MoveContainerAsync(int sort, byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    ValueTask<GenericActionResult> PickupGroundItemAsync(byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    ValueTask<GenericActionResult> PayTeleportTollAsync(byte[] data, int characterId,
        CancellationToken cancellationToken);

    ValueTask<GenericActionResult> LearnSkillAsync(int sort, byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    ValueTask<GenericActionResult> UpgradeSkillAsync(byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    ValueTask<GenericActionResult> SellToNpcShopAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken);

    ValueTask<GenericActionResult> BuyFromNpcShopAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IGenericActionService" />
public sealed class GenericActionService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    QuestCatalog questCatalog,
    ILogger<GenericActionService> logger)
    : IGenericActionService
{
    public async ValueTask<GenericActionResult> MoveContainerAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        if (!ContainerMatrix.IsImplementedContainerMoveSort(sort))
            return GenericActionResult.Failed;

        if (!DefaultPData.TryRead(data, out var move))
            return GenericActionResult.Aborted;

        if (!ContainerMatrix.TryResolveContainers(sort, move.Page1, move.Page2, out var fromContainer,
                out var toContainer))
            return GenericActionResult.Failed;

        // Bounds-checked before any byte cast: an out-of-range Index must never be truncated into a byte that
        // could accidentally alias a real slot.
        var sourceStack = ContainerMatrix.IsValidSlot(fromContainer, move.Index1)
            ? state.Inventory.GetSlot(fromContainer, (byte)move.Index1)
            : null;
        var destinationStack = ContainerMatrix.IsValidSlot(toContainer, move.Index2)
            ? state.Inventory.GetSlot(toContainer, (byte)move.Index2)
            : null;

        var sourceIsStackable = sourceStack is { } source &&
                                worldData.ItemsById.TryGetValue(source.ItemId, out var sourceDefinition) &&
                                ContainerMatrix.IsStackableSort(sourceDefinition.Item.Sort);

        var resolved = ContainerMatrix.ResolveMove(fromContainer, move.Index1, move.Quantity1, toContainer,
            move.Index2, sourceStack, destinationStack, sourceIsStackable);

        if (!resolved.Succeeded)
            return GenericActionResult.Failed;

        if (resolved.Outcome == ContainerMatrix.MoveOutcome.NoOp)
            return GenericActionResult.Succeeded;

        var projected = ContainerMatrix.ApplyMove(resolved, fromContainer, move.Index1,
            state.Inventory.GetContainer(fromContainer), toContainer, move.Index2,
            state.Inventory.GetContainer(toContainer));

        EffectiveStats? updatedStats = null;
        if (fromContainer == ContainerMatrix.Equipment || toContainer == ContainerMatrix.Equipment)
        {
            var equipmentContainer = fromContainer == ContainerMatrix.Equipment ? projected.From : projected.To;
            var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt,
                state.StatDex, state.Level, state.Tribe, state.Title, state.Halo, state.RebirthCount);

            // Pet stat contribution uses the PROJECTED equipment but the still-current growth/activity -- a pet
            // swap within one request can transiently keep the old pet's growth for the new one until the next
            // stat-affecting event self-corrects it (documented, minor, non-observable window).
            var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
                ? petStack.ItemId
                : 0;
            var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
                worldData.ItemsById);

            updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
                pet: petContribution);
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
                new InventoryZoneCommand(characterId, containers, updatedStats),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped container-move mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> PickupGroundItemAsync(byte[] data, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
            return GenericActionResult.Aborted;

        if (move.Page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)move.Page2, move.Index2) ||
            move.XPost2 is < 0 or > 7 || move.YPost2 is < 0 or > 7)
            return GenericActionResult.Aborted;

        var claimOutcome = zone.TryClaimGroundItem(move.Page1, unchecked((uint)move.Index1), state.Name,
            null, state.PosX, state.PosY, state.PosZ, out var groundItem);

        if (claimOutcome != GroundItemClaimOutcome.Success || groundItem is null)
            return GenericActionResult.Failed;

        if (!worldData.ItemsById.TryGetValue(groundItem.ItemId, out var itemDefinition))
            return GenericActionResult.Aborted;

        var destinationContainer = (byte)move.Page2;
        var destinationSlot = (byte)move.Index2;
        var existingStack = state.Inventory.GetSlot(destinationContainer, destinationSlot);

        var resolved = GroundItemPickupPolicy.Resolve(itemDefinition, groundItem, existingStack);
        if (!resolved.Succeeded)
            return GenericActionResult.Failed;

        if (resolved.Outcome == GroundItemPickupPolicy.Outcome.Money)
        {
            try
            {
                await characters.AdjustMoneyAsync(characterId, resolved.MoneyAmount, 0, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Character {CharacterId} ground-item money pickup AdjustMoneyAsync failed", characterId);
                return GenericActionResult.Aborted;
            }

            return GenericActionResult.Succeeded;
        }

        var projectedContainer = state.Inventory.GetContainer(destinationContainer)
            .SetItem(destinationSlot, resolved.NewSlot!.Value);

        await characters.ReplaceContainerAsync(characterId, destinationContainer, ToTvps(projectedContainer),
            cancellationToken);

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot(destinationContainer, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped pickup mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        // Quest pickup hook: a pure notification (no quest-state mutation), sent only when the picked-up item
        // matches the active qSort-2 quest's target and the quest is still in-progress at this instant. Reads
        // state.Inventory AFTER the mirror above so it observes the just-picked-up item.
        var notifyQuestProgress = false;
        if (state.QuestActiveFlag == 1 && state.QuestSort == 2 && state.QuestTargetPhase == groundItem.ItemId)
        {
            bool HasItem(int itemId)
            {
                return state.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values
                           .Any(s => s.ItemId == itemId) ||
                       state.Inventory.GetContainer(ContainerMatrix.InventoryPage1).Values.Any(s => s.ItemId == itemId);
            }

            var progress = new QuestProgress(state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort,
                state.QuestTargetPhase, state.QuestKillCounter);
            if (QuestStateMachine.ComputePresentState(progress, state.Tribe, state.Level, questCatalog, HasItem) ==
                QuestStateMachine.StateInProgress)
                notifyQuestProgress = true;
        }

        return new GenericActionResult(GenericActionStatus.Succeeded, notifyQuestProgress);
    }

    /// <summary>
    ///     tSort 207 -- paying-NPC instant teleport toll. Carries only the toll amount; the actual zone transfer
    ///     is the separate CZ_DEMAND_ZONE_SERVER_INFO_2 (opcode 20, Sort=5) which <c>ZoneMoveHandler</c> already
    ///     accepts generically. This action's job is gating the transfer on a successful money debit first.
    /// </summary>
    public async ValueTask<GenericActionResult> PayTeleportTollAsync(byte[] data, int characterId,
        CancellationToken cancellationToken)
    {
        if (!TeleportTollData.TryRead(data, out var toll))
            return GenericActionResult.Aborted;

        if (toll.Money is < 0 or > 100_000_000)
            return GenericActionResult.Aborted;

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

        return GenericActionResult.Succeeded;
    }

    /// <summary>tSort 202/233 -- learn a new skill from an NPC's skill-tree offer.</summary>
    public async ValueTask<GenericActionResult> LearnSkillAsync(int sort, byte[] data, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        if (!NpcSkillLearnData.TryRead(data, out var request))
            return GenericActionResult.Aborted;

        var arrayKind = sort == 202 ? SkillLearnResolver.SkillTree1 : SkillLearnResolver.SkillTree2;
        var functionId = sort == 202 ? NpcFunctionGate.LearnSkillTree1 : NpcFunctionGate.LearnSkillTree2;

        if (!worldData.NpcsById.TryGetValue(request.NpcId, out var npc) ||
            !worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition) ||
            !NpcFunctionGate.IsAvailable(zoneDefinition, worldData, functionId, state.PosX, state.PosY, state.PosZ))
            return GenericActionResult.Aborted;

        worldData.SkillsById.TryGetValue(request.SkillId, out var skillDefinition);

        var result = SkillLearnResolver.ResolveLearn(npc.SkillOffers, arrayKind, request.SkillId, skillDefinition,
            state.LearnedSkills, state.SkillPoints);

        if (!result.Success)
            return GenericActionResult.Aborted;

        var learned = new LearnedSkill(request.SkillId, result.Cost);
        var newSkillPoints = state.SkillPoints - result.Cost;

        await characters.UpsertSkillSlotAsync(characterId, result.Slot, request.SkillId, result.Cost,
            cancellationToken);

        if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, result.Slot, learned, newSkillPoints)))
            logger.LogError(
                "Zone {MapId} skill inbox full: dropped learn mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return GenericActionResult.Succeeded;
    }

    /// <summary>tSort 203 -- upgrade an already-learned skill's grade. No NPC-proximity gate applies here.</summary>
    public async ValueTask<GenericActionResult> UpgradeSkillAsync(byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken)
    {
        if (!SkillUpgradeData.TryRead(data, out var request))
            return GenericActionResult.Aborted;

        var learned = default(LearnedSkill);
        SkillDefinition? skillDefinition = null;
        if (request.SkillIndex is >= 0 and < SkillLearnResolver.MaxSlots &&
            state.LearnedSkills.TryGetValue((byte)request.SkillIndex, out learned))
            worldData.SkillsById.TryGetValue(learned.SkillId, out skillDefinition);

        var result = SkillLearnResolver.ResolveUpgrade(request.SkillIndex, state.LearnedSkills, skillDefinition,
            state.SkillPoints);

        if (!result.Success)
            return GenericActionResult.Aborted;

        var slot = (byte)request.SkillIndex;
        var upgraded = new LearnedSkill(learned.SkillId, result.NewGrade);
        var newSkillPoints = state.SkillPoints - 1;

        await characters.UpsertSkillSlotAsync(characterId, slot, learned.SkillId, result.NewGrade,
            cancellationToken);

        if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, slot, upgraded, newSkillPoints)))
            logger.LogError(
                "Zone {MapId} skill inbox full: dropped upgrade mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return GenericActionResult.Succeeded;
    }

    /// <summary>tSort 212/252 -- sell to an NPC shop.</summary>
    public async ValueTask<GenericActionResult> SellToNpcShopAsync(Zone zone, PlayerRuntimeState state,
        int characterId, DefaultPData move, CancellationToken cancellationToken)
    {
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId) ||
            !worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition) ||
            !NpcFunctionGate.IsAvailable(zoneDefinition, worldData, NpcFunctionGate.NpcShop, state.PosX, state.PosY,
                state.PosZ))
            return GenericActionResult.Aborted;

        var page1 = move.Page1;
        var index1 = move.Index1;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1))
            return GenericActionResult.Aborted;

        var sourceStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (sourceStack is not { } source || !worldData.ItemsById.TryGetValue(source.ItemId, out var itemDefinition))
            return GenericActionResult.Aborted;

        var resolved = NpcShopPolicy.ResolveSell(itemDefinition, source, move.Quantity1);
        if (!resolved.Succeeded)
            return GenericActionResult.Aborted;

        var currentContainer = state.Inventory.GetContainer((byte)page1);
        var projectedContainer = resolved.RemainingSourceStack is { } remaining
            ? currentContainer.SetItem((byte)index1, remaining)
            : currentContainer.Remove((byte)index1);

        try
        {
            await characters.AdjustMoneyAndReplaceContainerAsync(characterId, resolved.MoneyGained, 0, (byte)page1,
                ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} NPC-shop-sell AdjustMoneyAndReplaceContainerAsync failed (treated as money-cap breach)",
                characterId);
            return GenericActionResult.Aborted;
        }

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped NPC-sell mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return GenericActionResult.Succeeded;
    }

    /// <summary>
    ///     tSort 215 -- buy from an NPC shop. <paramref name="move" />.Page1/Index1 repurpose the wire shape as
    ///     (NpcId, ItemId), not an inventory slot.
    /// </summary>
    public async ValueTask<GenericActionResult> BuyFromNpcShopAsync(Zone zone, PlayerRuntimeState state,
        int characterId, DefaultPData move, CancellationToken cancellationToken)
    {
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId) ||
            !worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition) ||
            !NpcFunctionGate.IsAvailable(zoneDefinition, worldData, NpcFunctionGate.NpcShop, state.PosX, state.PosY,
                state.PosZ))
            return GenericActionResult.Aborted;

        if (!worldData.NpcsById.TryGetValue(move.Page1, out var npc) ||
            !worldData.ItemsById.TryGetValue(move.Index1, out var itemDefinition))
            return GenericActionResult.Aborted;

        var page2 = move.Page2;
        var index2 = move.Index2;
        if (page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page2, index2))
            return GenericActionResult.Aborted;

        var destinationSlot = state.Inventory.GetSlot((byte)page2, (byte)index2);

        var resolved = NpcShopPolicy.ResolveBuy(npc, itemDefinition, move.Quantity1, destinationSlot, state.Level,
            zone.MapId, state.ContributionPoints);

        if (!resolved.Succeeded)
            return resolved.IsCleanFailure ? GenericActionResult.Failed : GenericActionResult.Aborted;

        var projectedContainer = state.Inventory.GetContainer((byte)page2)
            .SetItem((byte)index2, resolved.NewDestinationStack!.Value);

        try
        {
            await characters.AdjustMoneyAndReplaceContainerAsync(characterId, -resolved.MoneyCost, 0, (byte)page2,
                ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} NPC-shop-buy AdjustMoneyAndReplaceContainerAsync failed (treated as insufficient funds)",
                characterId);
            return GenericActionResult.Aborted;
        }

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page2, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped NPC-buy mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        // Contribution Points (BuyCost2) aren't debited by the SQL call above -- unlike Money, they're a
        // write-behind field on PlayerRuntimeState (same posture as CraftLegendaryPetHandler's CP spend), so the
        // in-memory mirror IS the durable-enough record until the next flush.
        if (resolved.CpCost > 0 &&
            !await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, state.ContributionPoints - resolved.CpCost),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped CP mirror for character {CharacterId} after NPC-shop-buy",
                zone.MapId, characterId);

        return GenericActionResult.Succeeded;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
