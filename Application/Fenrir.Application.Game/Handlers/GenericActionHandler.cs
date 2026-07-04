using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
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
///     op19, CZ_PROCESS_DATA_SEND (contracts/03_inventory_craft.md, report 04_mega_switches.md §1) -- the
///     catch-all level-2 dispatch on <c>tSort</c>. Phase C/V1 implemented the "most fundamental" container
///     moves: 208 (inventory&lt;-&gt;inventory), 210 (inventory-&gt;equipment), 213 (equipment-&gt;inventory)
///     -- via <see cref="ContainerMatrix" />'s pure policy. Phase C/V5 (NPC &amp; Economy) adds: 201 (ground
///     pickup, <see cref="HandlePickupAsync" />), 207 (paying-NPC teleport toll,
///     <see cref="HandleTeleportTollAsync" />), 202/233 (learn skill, <see cref="HandleSkillLearnAsync" />),
///     203 (upgrade skill, <see cref="HandleSkillUpgradeAsync" />), 212/252 (sell to NPC,
///     <see cref="HandleNpcShopSellAsync" />), 215 (buy from NPC, <see cref="HandleNpcShopBuyAsync" />). Every
///     OTHER tSort the legacy switch itself recognizes (container-move families this pass doesn't cover --
///     Store/Trade/Bank/Hotkey-assign/1B-money/pet-bag/rune, GM 501-528, scripted duel 598-603, pet/maintenance
///     700-701, progression 204-206/235-237/239) gets a clean <c>tResult</c> FAILURE reply instead of a
///     disconnect -- see <see cref="ContainerMatrix.IsImplementedContainerMoveSort" />'s own remarks and this
///     task's openIssues for the full unimplemented list. A tSort absent from EVERY legacy family at all is the
///     only case that still gets <see cref="ClientSession.Abort" /> (anti-fuzzing, matching the legacy's own
///     <c>default:</c> branch).
/// </summary>
/// <remarks>
///     D7 regime (b): the affected container(s) are persisted SYNCHRONOUSLY -- via
///     <see cref="CharacterRepository.ReplaceContainerAsync" /> for a same-container move, or
///     <see cref="CharacterRepository.ReplaceTwoContainersAsync" /> (ONE transaction covering both) for a
///     cross-container move -- BEFORE the client ever sees a success reply: item state is a value object,
///     never write-behind (mission brief, "comme l'argent"), and a cross-container move must never be able to
///     durably remove an item from its source without also durably adding it to its destination. Once durable,
///     the ALREADY-COMPUTED result (containers + recomputed stats if Equipment was touched) is posted to
///     <see cref="Zone" /> via <see cref="Zone.PostInventoryCommand" /> so the zone's own tick -- the single
///     mutator of <see cref="PlayerRuntimeState" /> -- can mirror it; this handler never touches
///     <see cref="PlayerRuntimeState.Inventory" />/<see cref="PlayerRuntimeState.Stats" /> directly.
/// </remarks>
public sealed class GenericActionHandler(
    CharacterRepository characters,
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

        // Benign staleness (mid-handoff/disconnect) -- same defensive posture as every other InWorld handler.
        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // PlayerRuntimeState.EconomyActionLock's own remarks: serializes this WHOLE dispatch (every sort this
        // handler covers, not just the money-bearing ones -- SkillPoints/Inventory are shared, racy state
        // across ALL of them) per character, closing the item/money duplication window a burst of concurrent
        // requests for the same character could otherwise open.
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
            // Anti-fuzzing default case (report 04 §PROCESS_DATA switch): a tSort that exists in NO legacy
            // family at all, not merely one Fenrir hasn't wired up yet.
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // tSort 201 (ground -> inventory pickup, report 04 line 40 / 05 §5) does not fit the container-move
        // (from,to) shape the rest of this handler implements -- its "from" is the zone's ground-item pool,
        // not a player container. Handled by a dedicated branch/policy (GroundItemPickupPolicy) instead.
        if (sort == 201)
        {
            await HandlePickupAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
            return;
        }

        // V5 NPC & Economy -- tSort 207: paying-NPC instant teleport toll (money-only, no container touched).
        if (sort == 207)
        {
            await HandleTeleportTollAsync(packet, session, zoneSession, characterId, cancellationToken);
            return;
        }

        // V5 NPC & Economy -- tSort 202/233: learn a new skill from an NPC's skill-tree offer.
        if (sort is 202 or 233)
        {
            await HandleSkillLearnAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
            return;
        }

        // V5 NPC & Economy -- tSort 203: upgrade an already-learned skill (no NPC-proximity gate, verified).
        if (sort == 203)
        {
            await HandleSkillUpgradeAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
            return;
        }

        // V5 NPC & Economy -- tSort 212/252/215: NPC-shop sell/buy. Same DEFAULT_PDATA_RECV wire shape as the
        // container-move family below, but NEITHER side is a ContainerMatrix container (sell's destination is
        // the NPC's own catalog; buy's "from" (Page1=NpcId, Index1=ItemId) isn't an inventory slot at all --
        // see NpcShopPolicy's own remarks), so this is handled as its own branch rather than folded into
        // ContainerMatrix.TryResolveContainers.
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
            SendResult(session, sort, packet.Data, success: false);
            return;
        }

        // Structurally always 28 bytes available (Data is a fixed 130-byte array) -- defensive nonetheless,
        // mirroring the legacy's own "recast tData, bail on incoherence" contract.
        if (!DefaultPData.TryRead(packet.Data, out var move))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!ContainerMatrix.TryResolveContainers(sort, move.Page1, move.Page2, out var fromContainer,
                out var toContainer))
        {
            SendResult(session, sort, packet.Data, success: false);
            return;
        }

        // Bounds-checked BEFORE any byte cast/dictionary lookup -- a client-controlled Index1/Index2 outside
        // [0, container max] must never be truncated into a byte that could accidentally alias a real slot.
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
            SendResult(session, sort, packet.Data, success: false);
            return;
        }

        if (resolved.Outcome == ContainerMatrix.MoveOutcome.NoOp)
        {
            // Same slot to itself -- nothing actually changed, no SQL, no zone command needed.
            SendResult(session, sort, packet.Data, success: true);
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

            // Server Logic V9 Progression: pet stat contribution, computed against the PROJECTED equipment
            // (so unequipping the pet immediately zeroes it) but the STILL-CURRENT growth/activity -- a pet
            // SWAP (unequip pet A, equip pet B in the SAME session) sees the reset only once Zone's own
            // tick mirrors this move (Zone.ApplyInventoryCommand), so this ONE recompute can transiently use
            // pet A's leftover growth for pet B until the NEXT stat-affecting event self-corrects it --
            // documented, minor, non-observable-outside-that-one-window open issue.
            var petItemId = equipmentContainer.TryGetValue(Pets.PetSlots.EquipmentSlot, out var petStack)
                ? petStack.ItemId
                : 0;
            var petContribution = Pets.PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
                worldData.ItemsById);

            updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
                pet: petContribution);
        }

        // D7 regime (b): synchronous, awaited, BEFORE the client ever sees success -- see class remarks. A
        // cross-container move commits BOTH containers in ONE transaction (usp_CharacterItems_ReplaceTwoContainers):
        // two independent ReplaceContainerAsync calls here would let a fault between them durably remove an
        // item from its source without ever durably adding it to its destination -- a silent, permanent item
        // loss (review finding, Phase C/V2 integration). A same-container move (toContainer == fromContainer)
        // never had that risk -- projected.To already reflects fromContainer's own final state in that case.
        if (toContainer == fromContainer)
            await characters.ReplaceContainerAsync(characterId, fromContainer, ToTvps(projected.From),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, fromContainer, ToTvps(projected.From),
                toContainer, ToTvps(projected.To), cancellationToken);

        SendResult(session, sort, packet.Data, success: true);

        var containers = toContainer == fromContainer
            ? ImmutableArray.Create(new InventoryContainerSnapshot(fromContainer, projected.From))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot(fromContainer, projected.From),
                new InventoryContainerSnapshot(toContainer, projected.To));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, updatedStats),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped container-move mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>
    ///     tSort 201 -- ground-item pickup (report 04 line 40 / 05 §5, verified against
    ///     <c>MyWork::ProcessForGetItem</c>, <c>Server/ts25zone/S04_MyWork05.cpp:250-373</c>). D7 regime (b):
    ///     the destination container (or the money balance) is persisted SYNCHRONOUSLY, exactly like
    ///     <see cref="HandleAsync" />'s own container-move path, BEFORE the client ever sees success.
    /// </summary>
    /// <remarks>
    ///     OPEN ISSUE: if the SQL write (or the <see cref="WorldDataCache.ItemsById" /> lookup, which should
    ///     never actually miss for an item this pass's own drop pipeline created) fails AFTER
    ///     <see cref="Zone.TryClaimGroundItem" /> already atomically removed the item from the zone's ground
    ///     pool, the item is lost rather than restored -- a deliberate, safe (never duplicates) simplification
    ///     for what should be an exceedingly rare fault window; see <see cref="Inventory.GroundItemPickupPolicy" />'s
    ///     own remarks for the analogous, more likely "destination occupied" case.
    /// </remarks>
    private async ValueTask HandlePickupAsync(GenericActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        // Structurally always 28 bytes available (Data is a fixed 130-byte array) -- defensive nonetheless,
        // mirroring the legacy's own "recast tData, bail on incoherence" contract (same guard the
        // container-move path above already applies).
        if (!DefaultPData.TryRead(packet.Data, out var move))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Structural bounds the legacy Quit()s on (S04_MyWork05.cpp:265-276): tPage2/tIndex2 must address a
        // real inventory slot, tXPost2/tYPost2 must be a legal 0-7 sub-grid coordinate. Fenrir's flat-slot
        // ContainerMatrix does not use the sub-grid for addressing (see GroundItemPickupPolicy's own class
        // remarks), but the range check is still reproduced here for anti-fuzzing parity with the source.
        if (move.Page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)move.Page2, move.Index2) ||
            move.XPost2 is < 0 or > 7 || move.YPost2 is < 0 or > 7)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var claimOutcome = zone.TryClaimGroundItem(move.Page1, unchecked((uint)move.Index1), state.Name,
            claimantPartyName: null, state.PosX, state.PosY, state.PosZ, out var groundItem);

        if (claimOutcome != GroundItemClaimOutcome.Success || groundItem is null)
        {
            // Matches the legacy's own soft-fail contract exactly: CheckPossibleGetItem/unique-number
            // mismatch/distance all just leave tResult at 1 (return TRUE), never Quit().
            SendResult(session, packet.Sort, packet.Data, success: false);
            return;
        }

        if (!worldData.ItemsById.TryGetValue(groundItem.ItemId, out var itemDefinition))
        {
            // Cannot happen from this pass's own drop pipeline (it only ever rolls real catalog ids) -- mirrors
            // the legacy's own "mITEM.Search returns NULL" Quit() guard rather than silently failing.
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var destinationContainer = (byte)move.Page2;
        var destinationSlot = (byte)move.Index2;
        var existingStack = state.Inventory.GetSlot(destinationContainer, destinationSlot);

        var resolved = GroundItemPickupPolicy.Resolve(itemDefinition, groundItem, existingStack);
        if (!resolved.Succeeded)
        {
            SendResult(session, packet.Sort, packet.Data, success: false);
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
                // Money-cap breach (unlikely for a ground-drop amount, but possible near the cap) or a transient
                // SQL fault -- review finding: this call previously had NO try/catch at all, unlike every other
                // money-touching branch in this handler, so an exception here would propagate unhandled instead
                // of failing this one pickup cleanly. The ground item was already atomically claimed/removed
                // from the zone's pool before this point (Zone.TryClaimGroundItem) -- matches this method's own
                // documented "lost rather than restored" open issue for a post-claim fault, not a new gap.
                logger.LogWarning(ex,
                    "Character {CharacterId} ground-item money pickup AdjustMoneyAsync failed", characterId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            SendResult(session, packet.Sort, packet.Data, success: true);
            return;
        }

        var projectedContainer = state.Inventory.GetContainer(destinationContainer)
            .SetItem(destinationSlot, resolved.NewSlot!.Value);

        await characters.ReplaceContainerAsync(characterId, destinationContainer, ToTvps(projectedContainer),
            cancellationToken);

        SendResult(session, packet.Sort, packet.Data, success: true);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(destinationContainer, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped pickup mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        // Server Logic V9 Progression -- quest pickup hook (report 04 line 40, verified S04_MyWork04.cpp:370):
        // a PURE notification (no quest-state mutation at all -- the actual qSort-2 completion condition is
        // the inventory-presence scan CZ_PROCESS_QUEST_SEND tSort=2 itself performs), sent only when the
        // picked-up item matches the active qSort-2 quest's target AND ReturnQuestPresentState() still
        // reports 2 (in-progress) at this exact instant -- reads state.Inventory AFTER the mirror above so
        // it observes the just-picked-up item, matching legacy's own post-SetInventory ordering.
        if (state.QuestActiveFlag == 1 && state.QuestSort == 2 && state.QuestTargetPhase == groundItem.ItemId)
        {
            bool HasItem(int itemId)
            {
                return state.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Any(s => s.ItemId == itemId) ||
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
    ///     tSort 207 -- paying-NPC instant teleport toll (report 04 line 46 / 05 §... "Argent → NPC
    ///     téléportation", verified inline at <c>S04_MyWork04.cpp:429-440</c>). Carries ONLY the toll amount;
    ///     the actual zone transfer is a SEPARATE, ALREADY-WIRED client action -- CZ_DEMAND_ZONE_SERVER_INFO_2
    ///     (opcode 20) <c>Sort=5</c> "paying NPC", which <c>ZoneMoveHandler</c> already accepts generically
    ///     (see that handler's own class remarks: Sort 2-12 all share the same wire-level validation). This
    ///     action's entire job is gating the transfer on a successful synchronous money debit FIRST.
    /// </summary>
    /// <remarks>
    ///     D7 regime (b): <c>AdjustMoneyAsync</c> is synchronous and awaited BEFORE the client ever sees
    ///     success. No NPC-proximity gate exists for this specific tSort in the verified source (unlike its
    ///     202/212/215/233 siblings) -- preserved as found, not a modeling gap.
    /// </remarks>
    private async ValueTask HandleTeleportTollAsync(GenericActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, int characterId, CancellationToken cancellationToken)
    {
        if (!TeleportTollData.TryRead(packet.Data, out var toll))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // S04_MyWork04.cpp:432 -- bounds [0,100000000]; Quit() on violation (no clean-fail path).
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
            // Insufficient balance -- matches the legacy's own Quit() (S04_MyWork04.cpp:432: wAvatar.aMoney <
            // r->tMoney), NOT a clean failure. Logged (review finding: this used to swallow every exception,
            // including a transient/unrelated SQL fault, silently as "insufficient balance") so an operator can
            // tell the two apart after the fact -- the client-facing Abort is unchanged either way.
            logger.LogWarning(ex,
                "Character {CharacterId} teleport-toll AdjustMoneyAsync failed (treated as insufficient balance)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendResult(session, packet.Sort, packet.Data, success: true);
    }

    /// <summary>
    ///     tSort 202/233 -- learn a new skill from an NPC's skill-tree offer (<c>ProcessForLearnSkill1</c>/
    ///     <c>ProcessForLearnSkill2</c>, verified <c>S04_MyWork05.cpp:375/3343</c>). EVERY failure in the
    ///     source is a <c>Quit()</c> -- there is no clean <c>tResult=1</c> path for this action at all.
    /// </summary>
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

        // Grade at learn time == the cost spent (legacy: skill1->sPoint = tSKILL_INFO->sLearnSkillPoint).
        var learned = new LearnedSkill(request.SkillId, result.Cost);
        var newSkillPoints = state.SkillPoints - result.Cost;

        await characters.UpsertSkillSlotAsync(characterId, result.Slot, request.SkillId, result.Cost,
            cancellationToken);

        SendResult(session, packet.Sort, packet.Data, success: true);

        if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, result.Slot, learned, newSkillPoints)))
            logger.LogError(
                "Zone {MapId} skill inbox full: dropped learn mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>
    ///     tSort 203 -- upgrade an already-learned skill's Grade (<c>ProcessForSkillUpgrade</c>, verified
    ///     <c>S04_MyWork05.cpp:555</c>). No <c>CheckNPCFunction</c> call site exists for this action (unlike
    ///     202/233) -- no NPC-proximity gate applies here, verified not a modeling gap.
    /// </summary>
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

        SendResult(session, packet.Sort, packet.Data, success: true);

        if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, slot, upgraded, newSkillPoints)))
            logger.LogError(
                "Zone {MapId} skill inbox full: dropped upgrade mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>
    ///     tSort 212/252 -- sell to an NPC shop (<c>ProcessForInventoryToNPCShop</c>, verified
    ///     <c>S04_MyWork05.cpp:1398</c>). EVERY rejection in the source is a <c>Quit()</c> -- no clean
    ///     <c>tResult=1</c> path exists for this action at all (see <see cref="NpcShopPolicy" />'s own remarks).
    /// </summary>
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
            // Money-cap breach (CheckOverMaximum) -- Quit()-worthy, verified (S04_MyWork05.cpp:1469/1489). Logged
            // (review finding) so a transient/unrelated SQL fault is distinguishable from a real cap breach.
            logger.LogWarning(ex,
                "Character {CharacterId} NPC-shop-sell AdjustMoneyAndReplaceContainerAsync failed (treated as money-cap breach)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendResult(session, packet.Sort, packet.Data, success: true);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped NPC-sell mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>
    ///     tSort 215 -- buy from an NPC shop (<c>ProcessForNPCShopToInventory</c>, verified
    ///     <c>S04_MyWork05.cpp:1716</c>). <paramref name="move" />.Page1/Index1 repurpose the generic wire
    ///     shape as (NpcId, ItemId) -- NOT an inventory slot (see <see cref="NpcShopPolicy" />'s own remarks).
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
                SendResult(session, packet.Sort, packet.Data, success: false);
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
            // Insufficient funds -- Quit()-worthy, verified (S04_MyWork05.cpp:1905-1910/1997-2002). Logged
            // (review finding) so a transient/unrelated SQL fault is distinguishable from real insufficient funds.
            logger.LogWarning(ex,
                "Character {CharacterId} NPC-shop-buy AdjustMoneyAndReplaceContainerAsync failed (treated as insufficient funds)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendResult(session, packet.Sort, packet.Data, success: true);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page2, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped NPC-buy mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>
    ///     ZC_PROCESS_DATA_RECV echoes the request's own <c>tData</c> blob back verbatim (report 04/contract
    ///     doc do not fully specify the response payload for this generic channel) -- a documented, reasonable
    ///     inference, not independently verified byte-for-bit (open issue).
    /// </summary>
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
