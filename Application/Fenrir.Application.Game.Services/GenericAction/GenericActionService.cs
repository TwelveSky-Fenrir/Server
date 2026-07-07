using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Stats;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Network.Serialization.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.GenericAction;

/// <inheritdoc cref="IGenericActionService" />
public sealed class GenericActionService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    QuestCatalog questCatalog,
    PartyRegistry partyRegistry,
    IEventLogRepository eventLog,
    ILogger<GenericActionService> logger)
    : IGenericActionService
{
    /// <summary>game.EventLog.EventCode for TimeExchange (legacy <c>GL_851_PLAYTIME_EXCHANGE</c>).</summary>
    private const short TimeExchangeEventCode = 1;

    private const byte TimeExchangeOutcome = 1;

    /// <summary>
    ///     game.EventLog.EventCode for a sale to an NPC shop, scoped independently within
    ///     <see cref="EventLogCategory.NpcShopTrade" /> -- see that category's own remarks for the numbering
    ///     scheme and legacy <c>GL_621_NSHOP_ITEM</c> citation.
    /// </summary>
    private const short NpcShopSellEventCode = 1;

    /// <summary>game.EventLog.EventCode for a purchase from an NPC shop -- see <see cref="NpcShopSellEventCode" />.</summary>
    private const short NpcShopBuyEventCode = 2;

    private const byte NpcShopTradeOutcome = 1;

    /// <summary>Server/ts25zone/S04_MyWork05.cpp:4808-4826 -- 694 teacher points per accrued play-time-event minute.</summary>
    private const int TeacherPointsPerPlayTimeMinute = 694;

    /// <summary>Server/ts25zone/S04_MyWork05.cpp:4808-4826 -- 400 pet experience per accrued play-time-event minute.</summary>
    private const int PetExperiencePerPlayTimeMinute = 400;

    /// <summary>
    ///     mDATA.aAction.aSort's idle/ready pose sentinel -- the same value already independently established by
    ///     the post-cure/expiry reset (Server/ts25zone/S07_MyGame04.cpp:449, mirrored by <c>Zone</c>'s own private
    ///     <c>IdleActionSort</c> in Zone.Stun.cs) and by the identical <c>ActionSort != 1</c> gate
    ///     <see cref="AutoBuffActivationResolver" /> and <c>MountStateResolver</c> already apply for their own,
    ///     unrelated actions.
    /// </summary>
    private const int IdleActionSort = 1;

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

        // Idle-pose precondition, both equip (210) and unequip (213) directions: ProcessForInventoryToEquip/
        // ProcessForEquipToInventory each soft-refuse (no disconnect) when the avatar's own currently-tracked
        // action isn't the idle/ready pose (Server/ts25zone/S04_MyWork05.cpp:1261-1265 for 210, :1575-1579 for
        // 213) -- what the non-idle codes represent (attack windup, stun, cast, etc.) wasn't itself observed in
        // those cited ranges, only that any value other than the idle sentinel fails this check. A well-formed
        // request the domain cleanly rejects (GenericActionResult.Failed --> wire Result=1), never a disconnect;
        // this runs ahead of the equip-legality gate and the transfer itself, matching both cited call sites'
        // own ordering. Never reached for 208 (plain inventory rearrange): neither side of a 208 move is ever
        // Equipment (see ContainerMatrix.TryResolveContainers).
        if ((toContainer == ContainerMatrix.Equipment || fromContainer == ContainerMatrix.Equipment) &&
            state.ActionSort != IdleActionSort)
            return GenericActionResult.Failed;

        // tSort 210 (Inventory->Equip) only: CheckPossibleEquipItem's tribe/slot-tag/level/rebirth/final-category
        // gate. Not applied to 208/213 -- neither ordinary inventory rearrange nor unequip re-checks equip
        // legality in the legacy (Server/ts25zone/S04_MyWork05.cpp:1234-1306 is the InventoryToEquip-only call
        // site). This is safe for the 213 (unequip, Equipment->Inventory) direction specifically because
        // ContainerMatrix.ResolveMove no longer swaps an occupied destination's contents into the vacated
        // Equipment slot -- an occupied, non-mergeable destination is now a hard reject for all 3 directions
        // (see ResolveMove's own remarks), so no path exists for an arbitrary/unvalidated item to reach
        // Equipment through this branch. resolved.Succeeded already guarantees sourceStack is non-null here.
        if (toContainer == ContainerMatrix.Equipment)
        {
            EquipItemValidationGate.EquipCandidate? candidate = null;
            if (worldData.ItemsById.TryGetValue(sourceStack!.Value.ItemId, out var equipDefinition))
            {
                var equipRow = equipDefinition.Item;
                candidate = new EquipItemValidationGate.EquipCandidate(equipRow.ItemId, equipRow.EquipInfo1,
                    equipRow.EquipInfo2, equipRow.LevelLimit, equipRow.MartialLevelLimit, equipRow.CheckSetItem,
                    equipRow.Sort);
            }

            // itemSortClassification stands in for the legacy ReturnItemSort(...) helper: its own derivation
            // logic is outside EquipItemValidationGate's citation range (see that type's own remarks), so 0 is
            // passed as a documented placeholder rather than a guessed formula -- it never matches any of the
            // hardcoded rebirth-12 classification codes, meaning only that one sub-check of the overall
            // rebirth gate is skipped here; tribe/slot-tag/level/final-category and the per-item-id/CheckSetItem
            // rebirth gates all run for real (including ItemNotFound when the item id itself doesn't resolve).
            // Flagged for a follow-up legacy finding, not guessed at.
            var equipOutcome = EquipItemValidationGate.Evaluate(candidate, 0,
                state.PreviousTribe, move.Index2, state.Level + state.Level2, state.RebirthCount);

            if (equipOutcome != EquipItemValidationGate.Outcome.Success)
                return GenericActionResult.Aborted;
        }

        var projected = ContainerMatrix.ApplyMove(resolved, fromContainer, move.Index1,
            state.Inventory.GetContainer(fromContainer), toContainer, move.Index2,
            state.Inventory.GetContainer(toContainer));

        EffectiveStats? updatedStats = null;
        if (fromContainer == ContainerMatrix.Equipment || toContainer == ContainerMatrix.Equipment)
        {
            var equipmentContainer = fromContainer == ContainerMatrix.Equipment ? projected.From : projected.To;
            var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt,
                state.StatDex, state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo,
                state.RebirthCount);

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

        // Resolved live from PartyRegistry at claim time (never a hardcoded absent value) -- see
        // PartyIdentityResolver's own remarks and GroundItemEntity.IsClaimableBy's rule 5/6 citations for why
        // this must be the claimant's real current party identity, not null, for a partied claim to ever
        // succeed.
        var claimantPartyName = PartyIdentityResolver.ResolveCurrentPartyName(partyRegistry, characterId,
            state.Name, memberId => zone.TryGetPlayer(memberId, out var member) ? member?.Name : null);

        var claimOutcome = zone.TryClaimGroundItem(move.Page1, unchecked((uint)move.Index1), state.Name,
            claimantPartyName, state.PosX, state.PosY, state.PosZ, out var groundItem);

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
        int accountId, int characterId, DefaultPData move, CancellationToken cancellationToken)
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

        // Logged only once AdjustMoneyAndReplaceContainerAsync above has durably committed -- an audit row must
        // never assert a sale the DB write didn't actually persist. Sold quantity is derived from the
        // before/after stack rather than echoing move.Quantity1 verbatim, since NpcShopPolicy.ResolveSell
        // ignores the requested quantity entirely for a non-stackable item (S04_MyWork05.cpp:1398-1542).
        var soldQuantity = source.Quantity - (resolved.RemainingSourceStack?.Quantity ?? 0);
        await eventLog.LogAsync(NpcShopSellEventCode, EventLogCategory.NpcShopTrade, accountId, characterId,
            null, null, null, resolved.MoneyGained, null, source.ItemId, soldQuantity, NpcShopTradeOutcome, null,
            cancellationToken);

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
        int accountId, int characterId, DefaultPData move, CancellationToken cancellationToken)
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

        // Logged only once AdjustMoneyAndReplaceContainerAsync above has durably committed -- an audit row must
        // never assert a purchase the DB write didn't actually persist. Purchased quantity is derived from the
        // before/after destination stack (rather than echoing move.Quantity1 verbatim) so a merge into an
        // already-occupied slot logs only the newly-added units, not the destination's post-merge total.
        var purchasedQuantity = resolved.NewDestinationStack!.Value.Quantity - (destinationSlot?.Quantity ?? 0);
        await eventLog.LogAsync(NpcShopBuyEventCode, EventLogCategory.NpcShopTrade, accountId, characterId,
            null, null, null, -(long)resolved.MoneyCost, null, itemDefinition.Item.ItemId, purchasedQuantity,
            NpcShopTradeOutcome, null, cancellationToken);

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

    /// <summary>
    ///     tSort 206 -- spends unspent stat points (aStatPoint) to raise Strength/Dexterity/Vitality/Intelligence.
    ///     Every rejection is a bare <see cref="GenericActionResult.Aborted" />: the legacy disconnects the
    ///     session for any illegal category code or unaffordable amount here, never a soft failure reply. Not
    ///     yet reachable from <c>GenericActionHandler</c>'s dispatch switch -- see
    ///     <see cref="IGenericActionService.AllocateStatPointAsync" />.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:705-791 (<c>ProcessForStatPlus</c> -- debit, attribute
    ///     increment, and terminal <c>SetBasicAbilityFromEquip</c> call, all completing as one unit once the
    ///     category code is legal and affordable) ; Server/ts25zone/S07_MyGame04.cpp:158-183
    ///     (<c>SetBasicAbilityFromEquip</c>'s refreshed field list -- mirrored here by
    ///     <see cref="EquipmentService.RecomputeStats" />; the mount/costume/stellar-core inputs it also reads
    ///     are not modeled by <see cref="StatCalculator" /> yet, same documented gap as that type's own remarks)
    ///     ; Server/ts25zone/S05_MyTransfer.cpp:544-559 (the acknowledgment echoes the raw request payload
    ///     unmodified -- this method never touches the wire payload itself, matching that). No stored-procedure
    ///     write happens here, matching the legacy's own lack of one for this tSort: the mutation is mirrored
    ///     onto <see cref="PlayerRuntimeState" /> via the same already-established <see cref="TribeProgressZoneCommand" />
    ///     channel <see cref="BuyFromNpcShopAsync" /> already uses for its own CP debit, which marks the
    ///     character dirty for the next write-behind progress flush.
    /// </remarks>
    public async ValueTask<GenericActionResult> AllocateStatPointAsync(int statSort, int addValue, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var resolved = StatAllocationResolver.Resolve(statSort, addValue, state.StatPoints);
        if (!resolved.Succeeded)
            return GenericActionResult.Aborted;

        var newVit = state.StatVit + (resolved.Stat == StatAllocationResolver.BaseStat.Vitality ? resolved.Amount : 0);
        var newStr = state.StatStr + (resolved.Stat == StatAllocationResolver.BaseStat.Strength ? resolved.Amount : 0);
        var newInt = state.StatInt +
                     (resolved.Stat == StatAllocationResolver.BaseStat.Intelligence ? resolved.Amount : 0);
        var newDex = state.StatDex + (resolved.Stat == StatAllocationResolver.BaseStat.Dexterity ? resolved.Amount : 0);

        var attributes = new CharacterBaseAttributes(newVit, newStr, newInt, newDex, state.Level, state.Tribe,
            state.PreviousTribe, state.Title, state.Halo, state.RebirthCount);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);

        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                StatVit: newVit, StatStr: newStr, StatInt: newInt, StatDex: newDex,
                StatPoints: resolved.NewStatPoints, UpdatedStats: updatedStats), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped stat-allocation mirror for character {CharacterId}",
                zone.MapId, characterId);

        return GenericActionResult.Succeeded;
    }

    /// <inheritdoc cref="IGenericActionService.TimeExchangeAsync" />
    public async ValueTask<GenericActionResult> TimeExchangeAsync(Zone zone, PlayerRuntimeState state,
        int accountId, int characterId, CancellationToken cancellationToken)
    {
        // The only precondition this action has (S04_MyWork05.cpp:4808-4826's own guard): fewer than 1
        // accrued minute is a silent no-op -- no log entry, no state change, still a success echo (see
        // GenericActionHandler.Respond).
        var accruedMinutes = state.PlayTimeEvent;
        if (accruedMinutes < 1)
            return GenericActionResult.Succeeded;

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

        // Audit log BEFORE either reward is actually applied (S04_MyWork05.cpp:4808-4826's own ordering) --
        // captures the PRE-grant pet-slot snapshot (identifier via ItemId, activity flag + stored
        // growth/experience in the payload), not the post-grant one.
        await eventLog.LogAsync(TimeExchangeEventCode, EventLogCategory.PlayTimeExchange, accountId, characterId,
            null, null, null, null, null, petItemId, teacherPointsGranted, TimeExchangeOutcome,
            $"PetActivity={preGrantPetActivity};PetGrowth={preGrantPetGrowth};PetExperienceGranted={petExperienceGranted}",
            cancellationToken);

        var newTeacherPoint = state.TeacherPoint + teacherPointsGranted;

        // Same combined guard Zone.CreditPetGrowthFromMonsterKill already applies for its own (unrelated)
        // call site: skip the pet-slot mirror entirely when nothing about it would actually change, rather
        // than posting a no-op mutation.
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

        // GrantedPetExperienceGrowth only when the credit was actually positive -- the reactivation-only
        // (CreditedAmount == 0) case still mutates PetActivity above but sends no experience-changed
        // notification, matching PetExperienceCreditResult's own remarks.
        var grantedPetGrowth = credited is { IsEligible: true, CreditedAmount: > 0 } ? credited.NewGrowth : (int?)null;
        return new GenericActionResult(GenericActionStatus.Succeeded, GrantedPetExperienceGrowth: grantedPetGrowth);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
