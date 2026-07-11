using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Quests;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Quests;

/// <remarks>
///     NPC-proximity gate on Accept/Complete is a deliberate <b>Fenrir hardening decision, NOT legacy parity</b>.
///     The legacy <c>PROCESS_QUEST_SEND</c> handler (Server/ts25zone/S04_MyWork02.cpp:7307-7559) gates neither
///     accept nor complete on distance -- it never resolves the step's anchor NPC number nor reads the player's
///     position, so a client can accept/complete from arbitrarily far from the quest NPC as long as the
///     quest-STATE guard passes. Fenrir closes that exploit here by reusing the same squared-distance rule legacy
///     already applies to the comparable NPC skill-learn/shop functions (<c>CheckNPCFunction</c>, radius
///     <see cref="NpcFunctionGate.ProximityRadius" />, Server/ts25zone/S07_MyGame07.cpp:230-257 +
///     Server/Header/mapcheck.h:17-20) via <see cref="NpcFunctionGate.CheckNpcProximity" />, keyed on the step's
///     concrete anchor NPC number rather than a menu-function.
///     <para>
///         The anchor-per-action mapping IS legacy-grounded: <c>ReturnQuestNextNPCNumber</c>
///         (Server/ts25zone/S07_MyGame04.cpp:1937-1975) resolves an in-progress step to its <c>qStartNPCNumber</c>
///         and a completed step to its <c>qEndNPCNumber</c> -- so Accept anchors on the accepted (next) step's
///         Start NPC and Complete anchors on the current step's End NPC.
///     </para>
///     <para>
///         The gate is intentionally <b>fail-open</b> in three cases, so it never disconnects a legitimate player
///         on a data gap: (1) the step declares no anchor NPC (number &lt;= 0); (2) the current map has no loaded
///         placement data; (3) the anchor NPC is not placed on the current map at all
///         (<see cref="NpcProximity.NpcNotInZone" />). Only case (3)'s residual -- accept/complete while on a map
///         the anchor NPC isn't on -- is left un-gated; tightening it to also require the correct map is an open
///         product decision (see this workstream's openQuestions). A proximity failure surfaces as a failed
///         <see cref="QuestActionResult" />, which <c>QuestProgressHandler</c> turns into a session disconnect --
///         the same outward signal legacy uses at every live <c>CheckNPCFunction</c> failure
///         (Server/ts25zone/S04_MyWork04.cpp:382-386). Receive/Exchange/Abandon are NOT gated: the contract does
///         not establish their anchors, and legacy gates none of them.
///     </para>
/// </remarks>
public sealed class QuestProgressService(
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    WorldDataCache worldData,
    QuestCatalog questCatalog,
    ILogger<QuestProgressService> logger)
    : IQuestProgressService
{
    /// <summary>
    ///     game.EventLog.EventCode for a quest-completion reward grant (tSort 2, "mission completed" -- see
    ///     <see cref="CompleteAsync" />), scoped independently within <see cref="EventLogCategory.ItemCreate" />;
    ///     EventCode is only ever caller-interpreted alongside its Category (see game.EventLog.sql's own
    ///     "app-owned numbering scheme" comment). Fires whenever completion actually grants something -- an
    ///     item deposit, and/or any nonzero money/experience/contribution-point/teacher-point reward -- not
    ///     only when an item was granted. Money has its own DeltaMoney column; experience, contribution
    ///     points, and teacher points have no dedicated EventLog column, so those three are packed into
    ///     Payload when any of them is nonzero.
    /// </summary>
    private const short QuestRewardEventCode = 1;

    public async ValueTask<QuestActionResult> AcceptAsync(QuestProgressRequest packet, PlayerRuntimeState state,
        Zone zone, int characterId, CancellationToken ct)
    {
        var edits = new ContainerEdits(state);

        bool HasItem(int itemId)
        {
            return edits.Get(ContainerMatrix.InventoryPage0).Values.Any(s => s.ItemId == itemId) ||
                   edits.Get(ContainerMatrix.InventoryPage1).Values.Any(s => s.ItemId == itemId);
        }

        var progress = CurrentProgress(state);

        var result = QuestStateMachine.Accept(progress, state.Tribe, state.Level, questCatalog, HasItem);
        if (!result.Success)
            return new QuestActionResult(false);

        // NPC-proximity hardening gate (Fenrir decision, not legacy parity -- see class remarks). Accept anchors
        // on the ACCEPTED (next) step's quest-giver, qStartNPCNumber.
        if (!IsNearQuestAnchorNpc(zone, state, progress.StepPermanent + 1, false))
        {
            logger.LogInformation(
                "Character {CharacterId} quest-accept rejected: not within range of the step {Step} start NPC",
                characterId, progress.StepPermanent + 1);
            return new QuestActionResult(false);
        }

        if (result.DepositItemId is { } depositItemId)
        {
            if (!TryValidateDepositSlot(packet.Page1, packet.Index1, packet.XPost, packet.YPost, edits,
                    out var container, out var slot))
                return new QuestActionResult(false);

            edits.Deposit(container, slot, new ItemStack(depositItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        await PersistAndMirrorAsync(zone, characterId, result.NewProgress, 0, 0, 0, 0, edits, ct);

        logger.LogInformation("Character {CharacterId} accepted quest step {StepPermanent} (activeFlag {ActiveFlag})",
            characterId, result.NewProgress.StepPermanent, result.NewProgress.ActiveFlag);

        return new QuestActionResult(true);
    }

    public async ValueTask<QuestActionResult> CompleteAsync(QuestProgressRequest packet, PlayerRuntimeState state,
        Zone zone, int characterId, int accountId, CancellationToken ct)
    {
        var edits = new ContainerEdits(state);

        bool HasItem(int itemId)
        {
            return edits.Get(ContainerMatrix.InventoryPage0).Values.Any(s => s.ItemId == itemId) ||
                   edits.Get(ContainerMatrix.InventoryPage1).Values.Any(s => s.ItemId == itemId);
        }

        var progress = CurrentProgress(state);

        byte? ItemSort(int itemId)
        {
            return worldData.ItemsById.TryGetValue(itemId, out var def) ? def.Item.Sort : null;
        }

        var result = QuestStateMachine.Complete(progress, state.Tribe, state.Level, questCatalog, HasItem, ItemSort);
        if (!result.Success)
            return new QuestActionResult(false);

        // NPC-proximity hardening gate (Fenrir decision, not legacy parity -- see class remarks). Complete anchors
        // on the CURRENT step's turn-in NPC, qEndNPCNumber.
        if (!IsNearQuestAnchorNpc(zone, state, progress.StepPermanent, true))
        {
            logger.LogInformation(
                "Character {CharacterId} quest-complete rejected: not within range of the step {Step} end NPC",
                characterId, progress.StepPermanent);
            return new QuestActionResult(false);
        }

        var itemRewardGranted = false;

        if (packet.Page1 != -1)
        {
            if (!TryValidateDepositSlot(packet.Page1, packet.Index1, packet.XPost, packet.YPost, edits,
                    out var container, out var slot))
                return new QuestActionResult(false);

            // Skip the deposit when no reward item is configured: an ItemId of 0 would be misread as an
            // empty slot by Fenrir's presence-keyed container model.
            if (result.RewardItemId > 0)
            {
                edits.Deposit(container, slot,
                    new ItemStack(result.RewardItemId, result.RewardItemQuantity, 0, 0, 0, 0, 0, 0, 0, 0, 0));
                itemRewardGranted = true;
            }
        }

        if (result.DeleteItemId > 0)
            edits.DeleteFirstMatch(result.DeleteItemId);

        await PersistAndMirrorAsync(zone, characterId, result.NewProgress, result.MoneyReward,
            result.ExperienceReward, result.ContributionPointsReward, result.TeacherPointReward, edits, ct);

        var hasNumericReward = result.MoneyReward != 0 || result.ExperienceReward != 0 ||
                               result.ContributionPointsReward != 0 || result.TeacherPointReward != 0;

        // Logged only once the quest-transition/container write above has durably committed, and only when
        // completion actually granted something -- an item deposit and/or a nonzero money/XP/CP/teacher-point
        // reward. A completion that grants literally nothing (all reward fields zero/absent) mints nothing to
        // audit here.
        if (itemRewardGranted || hasNumericReward)
            await eventLog.LogAsync(QuestRewardEventCode, EventLogCategory.ItemCreate, accountId, characterId,
                null, null, null,
                result.MoneyReward != 0 ? result.MoneyReward : null, null,
                itemRewardGranted ? result.RewardItemId : null,
                itemRewardGranted ? result.RewardItemQuantity : null,
                1,
                hasNumericReward
                    ? $"ExperienceReward={result.ExperienceReward};ContributionPointsReward={result.ContributionPointsReward};TeacherPointReward={result.TeacherPointReward}"
                    : null,
                ct);

        logger.LogInformation(
            "Character {CharacterId} completed quest step {StepPermanent}: money {MoneyReward}, experience {ExperienceReward}, item {RewardItemId}x{RewardItemQuantity} granted={ItemRewardGranted}",
            characterId, result.NewProgress.StepPermanent, result.MoneyReward, result.ExperienceReward,
            result.RewardItemId, result.RewardItemQuantity, itemRewardGranted);

        return new QuestActionResult(true);
    }

    public async ValueTask<QuestActionResult> ReceiveAsync(QuestProgressRequest packet, PlayerRuntimeState state,
        Zone zone, int characterId, CancellationToken ct)
    {
        var edits = new ContainerEdits(state);

        bool HasItem(int itemId)
        {
            return edits.Get(ContainerMatrix.InventoryPage0).Values.Any(s => s.ItemId == itemId) ||
                   edits.Get(ContainerMatrix.InventoryPage1).Values.Any(s => s.ItemId == itemId);
        }

        var progress = CurrentProgress(state);

        if (!QuestStateMachine.TryReceive(progress, state.Tribe, state.Level, questCatalog, HasItem,
                out var depositItemId))
            return new QuestActionResult(false);

        if (!TryValidateDepositSlot(packet.Page1, packet.Index1, packet.XPost, packet.YPost, edits,
                out var container, out var slot))
            return new QuestActionResult(false);

        edits.Deposit(container, slot, new ItemStack(depositItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var projected = edits.Get(container);
        await characters.ReplaceContainerAsync(characterId, container, ToTvps(projected), ct);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null), ct))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped quest-receive mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} received quest item {ItemId} into container {Container} slot {Slot}",
            characterId, depositItemId, container, slot);

        return new QuestActionResult(true);
    }

    public async ValueTask<QuestActionResult> ExchangeAsync(QuestProgressRequest packet, PlayerRuntimeState state,
        Zone zone, int characterId, CancellationToken ct)
    {
        var edits = new ContainerEdits(state);

        bool HasItem(int itemId)
        {
            return edits.Get(ContainerMatrix.InventoryPage0).Values.Any(s => s.ItemId == itemId) ||
                   edits.Get(ContainerMatrix.InventoryPage1).Values.Any(s => s.ItemId == itemId);
        }

        var progress = CurrentProgress(state);

        var result = QuestStateMachine.TryExchange(progress, state.Tribe, state.Level, questCatalog, HasItem);
        if (!result.Success)
            return new QuestActionResult(false);

        // Legacy quirk (ChangeQuestItem, Server/ts25zone/S07_MyGame04.cpp:2223-2244): the inventory
        // scan-and-swap's found/not-found signal is discarded by the caller. If the avatar no longer holds
        // the "before" item anywhere (already exchanged, traded away, or otherwise missing), no item is
        // physically changed, but quest-progress state still advances to "after exchange" exactly as if the
        // swap had succeeded -- there is no failure path tied to the swap itself.
        edits.TryReplaceFirstMatch(result.FromItemId, _ =>
            new ItemStack(result.ToItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        await PersistAndMirrorAsync(zone, characterId, result.NewProgress, 0, 0, 0, 0, edits, ct);

        logger.LogInformation(
            "Character {CharacterId} exchanged quest item {FromItemId} for {ToItemId} (step {StepPermanent})",
            characterId, result.FromItemId, result.ToItemId, result.NewProgress.StepPermanent);

        return new QuestActionResult(true);
    }

    public async ValueTask<QuestActionResult> AbandonAsync(QuestProgressRequest packet, PlayerRuntimeState state,
        Zone zone, int characterId, CancellationToken ct)
    {
        var edits = new ContainerEdits(state);

        bool HasItem(int itemId)
        {
            return edits.Get(ContainerMatrix.InventoryPage0).Values.Any(s => s.ItemId == itemId) ||
                   edits.Get(ContainerMatrix.InventoryPage1).Values.Any(s => s.ItemId == itemId);
        }

        var progress = CurrentProgress(state);

        if (!QuestStateMachine.TryAbandon(progress, state.Tribe, state.Level, questCatalog, HasItem,
                out var newProgress))
            return new QuestActionResult(false);

        await characters.ApplyQuestTransitionAsync(characterId, newProgress.StepPermanent, newProgress.ActiveFlag,
            newProgress.QSort, newProgress.TargetPhase, newProgress.KillCounter, 0,
            null, [], null, [], ct);

        if (!await zone.PostQuestCommandAndWaitAsync(
                new QuestZoneCommand(characterId, newProgress, 0, 0,
                    ImmutableArray<InventoryContainerSnapshot>.Empty), ct))
            logger.LogError(
                "Zone {MapId} quest inbox full: dropped abandon mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation("Character {CharacterId} abandoned quest step {StepPermanent}", characterId,
            newProgress.StepPermanent);

        return new QuestActionResult(true);
    }

    /// <summary>
    ///     Shared tail for Accept/Complete/Exchange: persist (quest-state + optional money + up to 2 containers) in ONE
    ///     transaction, then mirror onto the live PlayerRuntimeState.
    /// </summary>
    private async ValueTask PersistAndMirrorAsync(Zone zone, int characterId, QuestProgress newProgress,
        long deltaMoney, int experienceDelta, int contributionPointsDelta, int teacherPointDelta,
        ContainerEdits edits, CancellationToken ct)
    {
        var (container1, items1, container2, items2) = edits.ToTvpPairs();

        await characters.ApplyQuestTransitionAsync(characterId, newProgress.StepPermanent, newProgress.ActiveFlag,
            newProgress.QSort, newProgress.TargetPhase, newProgress.KillCounter, deltaMoney,
            container1, items1, container2, items2, ct);

        if (!await zone.PostQuestCommandAndWaitAsync(
                new QuestZoneCommand(characterId, newProgress, experienceDelta, contributionPointsDelta,
                    edits.ToSnapshots(), TeacherPointDelta: teacherPointDelta), ct))
            logger.LogError(
                "Zone {MapId} quest inbox full: dropped mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    private static QuestProgress CurrentProgress(PlayerRuntimeState state)
    {
        return new QuestProgress(state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort,
            state.QuestTargetPhase, state.QuestKillCounter);
    }

    /// <summary>
    ///     True when the player is within <see cref="NpcFunctionGate.ProximityRadius" /> of the anchor NPC for
    ///     <paramref name="anchorStep" /> (its <c>qEndNPCNumber</c> when <paramref name="useEndNpc" />, else its
    ///     <c>qStartNPCNumber</c>), OR when the gate is fail-open for a data gap. See the class remarks for the
    ///     three fail-open cases and why this is a Fenrir hardening decision rather than legacy parity. Rejects
    ///     (returns false) only when the anchor NPC IS placed on the current map but every placement is beyond
    ///     radius -- exactly the "accept/complete from anywhere on the same map" exploit the contract cites.
    /// </summary>
    private bool IsNearQuestAnchorNpc(Zone zone, PlayerRuntimeState state, int anchorStep, bool useEndNpc)
    {
        var quest = questCatalog.TryGet(state.Tribe, anchorStep);
        if (quest is null)
            return true;

        var anchorNpcNumber = useEndNpc ? quest.Quest.EndNPCNumber : quest.Quest.StartNPCNumber;
        if (anchorNpcNumber <= 0)
            return true;

        if (!worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition))
            return true;

        return NpcFunctionGate.CheckNpcProximity(zoneDefinition, anchorNpcNumber, state.PosX, state.PosY,
            state.PosZ) != NpcProximity.Far;
    }

    /// <summary>Bounds/occupancy validation shared by Accept/Complete/Receive.</summary>
    private static bool TryValidateDepositSlot(int page, int index, int xPost, int yPost, ContainerEdits edits,
        out byte container, out byte slot)
    {
        container = 0;
        slot = 0;

        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, index) ||
            xPost is < 0 or > 7 || yPost is < 0 or > 7)
            return false;

        container = (byte)page;
        slot = (byte)index;
        return !edits.Get(container).ContainsKey(slot);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

    /// <summary>
    ///     Accumulates projected container edits over the live inventory snapshot, so a delete-then-deposit
    ///     sequence on the same container merges into one final projection instead of racing itself.
    /// </summary>
    private sealed class ContainerEdits(PlayerRuntimeState state)
    {
        private static readonly byte[] InventoryContainers =
            [ContainerMatrix.InventoryPage0, ContainerMatrix.InventoryPage1];

        private readonly Dictionary<byte, ImmutableDictionary<byte, ItemStack>> _edits = new();

        public ImmutableDictionary<byte, ItemStack> Get(byte container)
        {
            return _edits.TryGetValue(container, out var edited) ? edited : state.Inventory.GetContainer(container);
        }

        public void Deposit(byte container, byte slot, ItemStack stack)
        {
            _edits[container] = Get(container).SetItem(slot, stack);
        }

        /// <summary>Scans page 0 then page 1, wipes the first matching slot; a no-op if not found anywhere.</summary>
        public void DeleteFirstMatch(int itemId)
        {
            foreach (var container in InventoryContainers)
            {
                var current = Get(container);
                var hitSlot = FindSlot(current, itemId);
                if (hitSlot is { } slot)
                {
                    _edits[container] = current.Remove(slot);
                    return;
                }
            }
        }

        /// <summary>Scans page 0 then page 1, replaces the first matching slot via <paramref name="transform" />.</summary>
        public bool TryReplaceFirstMatch(int itemId, Func<ItemStack, ItemStack> transform)
        {
            foreach (var container in InventoryContainers)
            {
                var current = Get(container);
                var hitSlot = FindSlot(current, itemId);
                if (hitSlot is { } slot)
                {
                    _edits[container] = current.SetItem(slot, transform(current[slot]));
                    return true;
                }
            }

            return false;
        }

        public ImmutableArray<InventoryContainerSnapshot> ToSnapshots()
        {
            if (_edits.Count == 0)
                return ImmutableArray<InventoryContainerSnapshot>.Empty;

            var builder = ImmutableArray.CreateBuilder<InventoryContainerSnapshot>(_edits.Count);
            foreach (var (container, content) in _edits)
                builder.Add(new InventoryContainerSnapshot(container, content));
            return builder.ToImmutable();
        }

        /// <summary>Splits the (at most 2) touched containers into parameter pairs; null container = "not touched".</summary>
        public (byte? Container1, List<CharacterItemSlotTvp> Items1, byte? Container2, List<CharacterItemSlotTvp> Items2
            )
            ToTvpPairs()
        {
            byte? c1 = null;
            List<CharacterItemSlotTvp> i1 = [];
            byte? c2 = null;
            List<CharacterItemSlotTvp> i2 = [];

            foreach (var (container, content) in _edits)
                if (c1 is null)
                {
                    c1 = container;
                    i1 = ToTvps(content);
                }
                else
                {
                    c2 = container;
                    i2 = ToTvps(content);
                }

            return (c1, i1, c2, i2);
        }

        private static byte? FindSlot(ImmutableDictionary<byte, ItemStack> container, int itemId)
        {
            foreach (var (slot, stack) in container)
                if (stack.ItemId == itemId)
                    return slot;
            return null;
        }
    }
}
