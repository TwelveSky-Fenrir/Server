using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Pets;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Application.Game.World.Npcs;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op19, CZ_PROCESS_DATA_SEND -- catch-all dispatch on tSort: container moves (inventory&lt;-&gt;inventory,
///     inventory&lt;-&gt;equipment), ground pickup, NPC teleport toll, skill learn/upgrade, and NPC shop buy/sell.
///     A tSort the legacy switch recognizes but this handler doesn't implement replies with a clean failure; a
///     tSort absent from every legacy family gets <see cref="ClientSession.Abort" /> (anti-fuzzing).
/// </summary>
/// <remarks>
///     Affected containers are persisted synchronously, before the client ever sees success. A cross-container
///     move commits both containers in one transaction so a mid-sequence fault can never durably remove an item
///     from its source without also adding it to its destination. Once durable, the result is posted to
///     <see cref="Zone" /> so the zone's own tick -- the sole mutator of <see cref="PlayerRuntimeState" /> --
///     can mirror it.
/// </remarks>
public sealed class GenericActionHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    QuestCatalog questCatalog,
    ILogger<GenericActionHandler> logger)
    : IAsyncPacketHandler<GenericActionRequest>
{
    public async ValueTask HandleAsync(GenericActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // Serializes this whole dispatch per character -- SkillPoints/Inventory/money are shared racy state
        // across every sort this handler covers, not just the money-bearing ones.
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await DispatchAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask DispatchAsync(GenericActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var sort = packet.Sort;

        if (!ContainerMatrix.IsKnownSort(sort))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // tSort 201 (ground pickup) doesn't fit the container-move (from,to) shape below -- its "from" is the
        // zone's ground-item pool, not a player container.
        if (sort == 201)
        {
            await HandlePickupAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
            return;
        }

        if (sort == 207)
        {
            await HandleTeleportTollAsync(packet, session, zoneSession, characterId, cancellationToken);
            return;
        }

        if (sort is 202 or 233)
        {
            await HandleSkillLearnAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
            return;
        }

        if (sort == 203)
        {
            await HandleSkillUpgradeAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
            return;
        }

        // tSort 212/252/215: NPC-shop sell/buy. Neither side is a ContainerMatrix container (sell's destination
        // is the NPC's own catalog; buy's Page1/Index1 repurpose the wire shape as NpcId/ItemId), so these are
        // handled as their own branch rather than folded into ContainerMatrix.TryResolveContainers.
        if (sort is 212 or 252 or 215)
        {
            if (!DefaultPData.TryRead(packet.Data, out var shopMove))
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (sort == 215)
                await HandleNpcShopBuyAsync(packet, session, zoneSession, zone, state, characterId, shopMove,
                    cancellationToken);
            else
                await HandleNpcShopSellAsync(packet, session, zoneSession, zone, state, characterId, shopMove,
                    cancellationToken);
            return;
        }

        if (!ContainerMatrix.IsImplementedContainerMoveSort(sort))
        {
            SendResult(session, sort, packet.Data, false);
            return;
        }

        if (!DefaultPData.TryRead(packet.Data, out var move))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!ContainerMatrix.TryResolveContainers(sort, move.Page1, move.Page2, out var fromContainer,
                out var toContainer))
        {
            SendResult(session, sort, packet.Data, false);
            return;
        }

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
        {
            SendResult(session, sort, packet.Data, false);
            return;
        }

        if (resolved.Outcome == ContainerMatrix.MoveOutcome.NoOp)
        {
            SendResult(session, sort, packet.Data, true);
            return;
        }

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

        SendResult(session, sort, packet.Data, true);

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
    }

    private async ValueTask HandlePickupAsync(GenericActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(packet.Data, out var move))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (move.Page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)move.Page2, move.Index2) ||
            move.XPost2 is < 0 or > 7 || move.YPost2 is < 0 or > 7)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var claimOutcome = zone.TryClaimGroundItem(move.Page1, unchecked((uint)move.Index1), state.Name,
            null, state.PosX, state.PosY, state.PosZ, out var groundItem);

        if (claimOutcome != GroundItemClaimOutcome.Success || groundItem is null)
        {
            SendResult(session, packet.Sort, packet.Data, false);
            return;
        }

        if (!worldData.ItemsById.TryGetValue(groundItem.ItemId, out var itemDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var destinationContainer = (byte)move.Page2;
        var destinationSlot = (byte)move.Index2;
        var existingStack = state.Inventory.GetSlot(destinationContainer, destinationSlot);

        var resolved = GroundItemPickupPolicy.Resolve(itemDefinition, groundItem, existingStack);
        if (!resolved.Succeeded)
        {
            SendResult(session, packet.Sort, packet.Data, false);
            return;
        }

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
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            SendResult(session, packet.Sort, packet.Data, true);
            return;
        }

        var projectedContainer = state.Inventory.GetContainer(destinationContainer)
            .SetItem(destinationSlot, resolved.NewSlot!.Value);

        await characters.ReplaceContainerAsync(characterId, destinationContainer, ToTvps(projectedContainer),
            cancellationToken);

        SendResult(session, packet.Sort, packet.Data, true);

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
                session.Send(new QuestProgressResponse { Sort = 7, Page = 0, Index = 0, XPost = 0, YPost = 0 });
        }
    }

    /// <summary>
    ///     tSort 207 -- paying-NPC instant teleport toll. Carries only the toll amount; the actual zone transfer
    ///     is the separate CZ_DEMAND_ZONE_SERVER_INFO_2 (opcode 20, Sort=5) which <c>ZoneMoveHandler</c> already
    ///     accepts generically. This action's job is gating the transfer on a successful money debit first.
    /// </summary>
    private async ValueTask HandleTeleportTollAsync(GenericActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, int characterId, CancellationToken cancellationToken)
    {
        if (!TeleportTollData.TryRead(packet.Data, out var toll))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (toll.Money is < 0 or > 100_000_000)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
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
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendResult(session, packet.Sort, packet.Data, true);
    }

    /// <summary>tSort 202/233 -- learn a new skill from an NPC's skill-tree offer.</summary>
    private async ValueTask HandleSkillLearnAsync(GenericActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (!NpcSkillLearnData.TryRead(packet.Data, out var request))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var arrayKind = packet.Sort == 202 ? SkillLearnResolver.SkillTree1 : SkillLearnResolver.SkillTree2;
        var functionId = packet.Sort == 202 ? NpcFunctionGate.LearnSkillTree1 : NpcFunctionGate.LearnSkillTree2;

        if (!worldData.NpcsById.TryGetValue(request.NpcId, out var npc) ||
            !worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition) ||
            !NpcFunctionGate.IsAvailable(zoneDefinition, worldData, functionId, state.PosX, state.PosY, state.PosZ))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        worldData.SkillsById.TryGetValue(request.SkillId, out var skillDefinition);

        var result = SkillLearnResolver.ResolveLearn(npc.SkillOffers, arrayKind, request.SkillId, skillDefinition,
            state.LearnedSkills, state.SkillPoints);

        if (!result.Success)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var learned = new LearnedSkill(request.SkillId, result.Cost);
        var newSkillPoints = state.SkillPoints - result.Cost;

        await characters.UpsertSkillSlotAsync(characterId, result.Slot, request.SkillId, result.Cost,
            cancellationToken);

        SendResult(session, packet.Sort, packet.Data, true);

        if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, result.Slot, learned, newSkillPoints)))
            logger.LogError(
                "Zone {MapId} skill inbox full: dropped learn mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>tSort 203 -- upgrade an already-learned skill's grade. No NPC-proximity gate applies here.</summary>
    private async ValueTask HandleSkillUpgradeAsync(GenericActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (!SkillUpgradeData.TryRead(packet.Data, out var request))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
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
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var slot = (byte)request.SkillIndex;
        var upgraded = new LearnedSkill(learned.SkillId, result.NewGrade);
        var newSkillPoints = state.SkillPoints - 1;

        await characters.UpsertSkillSlotAsync(characterId, slot, learned.SkillId, result.NewGrade,
            cancellationToken);

        SendResult(session, packet.Sort, packet.Data, true);

        if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, slot, upgraded, newSkillPoints)))
            logger.LogError(
                "Zone {MapId} skill inbox full: dropped upgrade mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>tSort 212/252 -- sell to an NPC shop.</summary>
    private async ValueTask HandleNpcShopSellAsync(GenericActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, DefaultPData move,
        CancellationToken cancellationToken)
    {
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId) ||
            !worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition) ||
            !NpcFunctionGate.IsAvailable(zoneDefinition, worldData, NpcFunctionGate.NpcShop, state.PosX, state.PosY,
                state.PosZ))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var page1 = move.Page1;
        var index1 = move.Index1;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var sourceStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (sourceStack is not { } source || !worldData.ItemsById.TryGetValue(source.ItemId, out var itemDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var resolved = NpcShopPolicy.ResolveSell(itemDefinition, source, move.Quantity1);
        if (!resolved.Succeeded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

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
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendResult(session, packet.Sort, packet.Data, true);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped NPC-sell mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>
    ///     tSort 215 -- buy from an NPC shop. <paramref name="move" />.Page1/Index1 repurpose the wire shape as
    ///     (NpcId, ItemId), not an inventory slot.
    /// </summary>
    private async ValueTask HandleNpcShopBuyAsync(GenericActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, DefaultPData move,
        CancellationToken cancellationToken)
    {
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId) ||
            !worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition) ||
            !NpcFunctionGate.IsAvailable(zoneDefinition, worldData, NpcFunctionGate.NpcShop, state.PosX, state.PosY,
                state.PosZ))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!worldData.NpcsById.TryGetValue(move.Page1, out var npc) ||
            !worldData.ItemsById.TryGetValue(move.Index1, out var itemDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var page2 = move.Page2;
        var index2 = move.Index2;
        if (page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page2, index2))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var destinationSlot = state.Inventory.GetSlot((byte)page2, (byte)index2);

        var resolved = NpcShopPolicy.ResolveBuy(npc, itemDefinition, move.Quantity1, destinationSlot, state.Level,
            zone.MapId);

        if (!resolved.Succeeded)
        {
            if (resolved.IsCleanFailure)
            {
                SendResult(session, packet.Sort, packet.Data, false);
                return;
            }

            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

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
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendResult(session, packet.Sort, packet.Data, true);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page2, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped NPC-buy mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    private static void SendResult(IPacketSession session, int sort, byte[] data, bool success)
    {
        session.Send(new GenericActionResponse { Result = success ? 0 : 1, Sort = sort, Data = data, RuneValue = 0 });
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
