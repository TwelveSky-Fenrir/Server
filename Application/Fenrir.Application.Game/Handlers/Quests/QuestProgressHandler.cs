using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Quests;

/// <summary>
///     CZ_PROCESS_QUEST_SEND (opcode 36, report 04 §5, verified byte-for-byte against
///     <c>Server/ts25zone/S04_MyWork02.cpp:7307-7563</c>) -- the 5-action quest state machine
///     (<see cref="QuestStateMachine" />). EVERY rejection in the legacy source is a <c>Quit()</c> -- there
///     is NO clean <c>tResult</c> failure path for this opcode at all, so every failed precondition here
///     aborts the session, matching that exactly.
/// </summary>
/// <remarks>
///     D7 regime (b): reward money and any inventory container touched are persisted SYNCHRONOUSLY, in ONE
///     transaction with the quest-state row itself (<c>usp_CharacterQuest_ApplyTransition</c>) -- BEFORE
///     the client ever sees success, exactly the same posture <c>GenericActionHandler</c>'s NPC-shop
///     buy/sell already established (a quest reward is the same class of economy action). CP/XP rewards
///     are write-behind (same regime as <c>Zone.GrantMonsterKillExperience</c>/ContributionPoints already
///     use) -- mirrored via <see cref="QuestZoneCommand" /> AFTER the SQL commit, awaited before this
///     handler's <see cref="PlayerRuntimeState.EconomyActionLock" /> is released (lesson: a duplication
///     race requires BOTH the read and the mirror to happen while holding the lock).
/// </remarks>
public sealed class QuestProgressHandler(
    CharacterRepository characters,
    WorldDataCache worldData,
    QuestCatalog questCatalog,
    ILogger<QuestProgressHandler> logger)
    : IAsyncPacketHandler<QuestProgressRequest>
{
    public async ValueTask HandleAsync(QuestProgressRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await DispatchAsync(packet, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask DispatchAsync(QuestProgressRequest packet, ZoneClientSession zoneSession, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        var edits = new ContainerEdits(state);
        bool HasItem(int itemId)
        {
            return edits.Get(ContainerMatrix.InventoryPage0).Values.Any(s => s.ItemId == itemId) ||
                   edits.Get(ContainerMatrix.InventoryPage1).Values.Any(s => s.ItemId == itemId);
        }

        var progress = CurrentProgress(state);

        switch (packet.Sort)
        {
            case 1:
                await HandleAcceptAsync(packet, zoneSession, zone, state, characterId, progress, edits, HasItem, ct);
                return;
            case 2:
                await HandleCompleteAsync(packet, zoneSession, zone, state, characterId, progress, edits, HasItem,
                    ct);
                return;
            case 3:
                await HandleReceiveAsync(packet, zoneSession, zone, state, characterId, progress, edits, HasItem,
                    ct);
                return;
            case 4:
                await HandleExchangeAsync(packet, zoneSession, zone, state, characterId, progress, edits, HasItem,
                    ct);
                return;
            case 5:
                await HandleAbandonAsync(packet, zoneSession, zone, state, characterId, progress, HasItem, ct);
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    /// <summary>tSort 1, "mission issuance" (S04_MyWork02.cpp:7314-7368).</summary>
    private async ValueTask HandleAcceptAsync(QuestProgressRequest packet, ZoneClientSession zoneSession, Zone zone,
        PlayerRuntimeState state, int characterId, QuestProgress progress, ContainerEdits edits,
        Func<int, bool> hasItem, CancellationToken ct)
    {
        var result = QuestStateMachine.Accept(progress, state.Tribe, state.Level, questCatalog, hasItem);
        if (!result.Success)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (result.DepositItemId is { } depositItemId)
        {
            if (!TryValidateDepositSlot(packet.Page1, packet.Index1, packet.XPost, packet.YPost, edits,
                    out var container, out var slot))
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            edits.Deposit(container, slot, new ItemStack(depositItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        await PersistAndMirrorAsync(characters, zone, logger, characterId, result.NewProgress, deltaMoney: 0,
            experienceDelta: 0, contributionPointsDelta: 0, teacherPointDelta: 0, edits, ct);

        SendEcho(zoneSession, packet);
    }

    /// <summary>tSort 2, "mission completed" (S04_MyWork02.cpp:7369-7452).</summary>
    private async ValueTask HandleCompleteAsync(QuestProgressRequest packet, ZoneClientSession zoneSession,
        Zone zone, PlayerRuntimeState state, int characterId, QuestProgress progress, ContainerEdits edits,
        Func<int, bool> hasItem, CancellationToken ct)
    {
        byte? ItemSort(int itemId)
        {
            return worldData.ItemsById.TryGetValue(itemId, out var def) ? def.Item.Sort : null;
        }

        var result = QuestStateMachine.Complete(progress, state.Tribe, state.Level, questCatalog, hasItem, ItemSort);
        if (!result.Success)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Page1 != -1)
        {
            if (!TryValidateDepositSlot(packet.Page1, packet.Index1, packet.XPost, packet.YPost, edits,
                    out var container, out var slot))
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            // Skip the deposit (but keep the already-validated slot check above) when no type-6 reward item
            // is actually configured for this quest -- see QuestStateMachine.Complete's own remarks; the
            // legacy would write ItemId 0 into the slot here, which Fenrir's presence-keyed container model
            // would otherwise misread as "occupied by nothing", a documented, safe adaptation.
            if (result.RewardItemId > 0)
                edits.Deposit(container, slot,
                    new ItemStack(result.RewardItemId, result.RewardItemQuantity, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        if (result.DeleteItemId > 0)
            edits.DeleteFirstMatch(result.DeleteItemId);

        await PersistAndMirrorAsync(characters, zone, logger, characterId, result.NewProgress, result.MoneyReward,
            result.ExperienceReward, result.ContributionPointsReward, result.TeacherPointReward, edits, ct);

        SendEcho(zoneSession, packet);
    }

    /// <summary>tSort 3, "mission receive" (S04_MyWork02.cpp:7453-7504) -- does NOT mutate quest state at all, only deposits an item; reuses the plain InventoryZoneCommand channel rather than QuestZoneCommand.</summary>
    private async ValueTask HandleReceiveAsync(QuestProgressRequest packet, ZoneClientSession zoneSession, Zone zone,
        PlayerRuntimeState state, int characterId, QuestProgress progress, ContainerEdits edits,
        Func<int, bool> hasItem, CancellationToken ct)
    {
        if (!QuestStateMachine.TryReceive(progress, state.Tribe, state.Level, questCatalog, hasItem,
                out var depositItemId))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!TryValidateDepositSlot(packet.Page1, packet.Index1, packet.XPost, packet.YPost, edits,
                out var container, out var slot))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        edits.Deposit(container, slot, new ItemStack(depositItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var projected = edits.Get(container);
        await characters.ReplaceContainerAsync(characterId, container, ToTvps(projected), ct);

        SendEcho(zoneSession, packet);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null), ct))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped quest-receive mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>tSort 4, "mission exchange" (S04_MyWork02.cpp:7505-7528).</summary>
    private async ValueTask HandleExchangeAsync(QuestProgressRequest packet, ZoneClientSession zoneSession,
        Zone zone, PlayerRuntimeState state, int characterId, QuestProgress progress, ContainerEdits edits,
        Func<int, bool> hasItem, CancellationToken ct)
    {
        var result = QuestStateMachine.TryExchange(progress, state.Tribe, state.Level, questCatalog, hasItem);
        if (!result.Success)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // ChangeQuestItem swaps the ITEM ID in place (same slot) and zeroes quantity/upgrade/serial --
        // verified S07_MyGame04.cpp:2223-2244. A missing source item cannot happen here (present-state
        // already required it via hasItem), but is handled defensively rather than assumed.
        if (!edits.TryReplaceFirstMatch(result.FromItemId, _ =>
                new ItemStack(result.ToItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await PersistAndMirrorAsync(characters, zone, logger, characterId, result.NewProgress, deltaMoney: 0,
            experienceDelta: 0, contributionPointsDelta: 0, teacherPointDelta: 0, edits, ct);

        SendEcho(zoneSession, packet);
    }

    /// <summary>tSort 5, "mission abandonment" (S04_MyWork02.cpp:7529-7555) -- no container touched.</summary>
    private async ValueTask HandleAbandonAsync(QuestProgressRequest packet, ZoneClientSession zoneSession, Zone zone,
        PlayerRuntimeState state, int characterId, QuestProgress progress, Func<int, bool> hasItem,
        CancellationToken ct)
    {
        if (!QuestStateMachine.TryAbandon(progress, state.Tribe, state.Level, questCatalog, hasItem,
                out var newProgress))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await characters.ApplyQuestTransitionAsync(characterId, newProgress.StepPermanent, newProgress.ActiveFlag,
            newProgress.QSort, newProgress.TargetPhase, newProgress.KillCounter, deltaMoney: 0,
            container1: null, items1: [], container2: null, items2: [], ct);

        SendEcho(zoneSession, packet);

        if (!await zone.PostQuestCommandAndWaitAsync(
                new QuestZoneCommand(characterId, newProgress, 0, 0,
                    ImmutableArray<InventoryContainerSnapshot>.Empty), ct))
            logger.LogError(
                "Zone {MapId} quest inbox full: dropped abandon mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>Shared tail for Accept/Complete/Exchange: persist (quest-state + optional money + up to 2 containers) in ONE transaction, then mirror onto the live PlayerRuntimeState.</summary>
    private static async ValueTask PersistAndMirrorAsync(CharacterRepository characters, Zone zone,
        ILogger logger, int characterId, QuestProgress newProgress, long deltaMoney, int experienceDelta,
        int contributionPointsDelta, int teacherPointDelta, ContainerEdits edits, CancellationToken ct)
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

    /// <summary>Bounds/occupancy validation shared by Accept(3/6)/Complete/Receive -- CheckInv(1) + 0..7 XPost/YPost + empty-slot, all Quit()-worthy on violation (verified S04_MyWork02.cpp:7331-7346 et al.).</summary>
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

    private static void SendEcho(IPacketSession session, QuestProgressRequest packet)
    {
        session.Send(new QuestProgressResponse
        {
            Sort = packet.Sort, Page = packet.Page1, Index = packet.Index1, XPost = packet.XPost,
            YPost = packet.YPost
        });
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

    /// <summary>
    ///     Accumulates up to 2 (only Inventory pages 0/1 are ever touched by a quest action) projected
    ///     container edits over the live <see cref="PlayerRuntimeState.Inventory" /> snapshot, so a
    ///     delete-then-deposit sequence that lands on the SAME container merges into ONE final projection
    ///     instead of racing itself.
    /// </summary>
    private sealed class ContainerEdits(PlayerRuntimeState state)
    {
        private readonly Dictionary<byte, ImmutableDictionary<byte, ItemStack>> _edits = new();

        public ImmutableDictionary<byte, ItemStack> Get(byte container)
        {
            return _edits.TryGetValue(container, out var edited) ? edited : state.Inventory.GetContainer(container);
        }

        public void Deposit(byte container, byte slot, ItemStack stack)
        {
            _edits[container] = Get(container).SetItem(slot, stack);
        }

        /// <summary>Mirrors DeleteQuestItem (S07_MyGame04.cpp:2246-2269): scans page 0 then page 1, wipes the FIRST matching slot, a no-op if not found anywhere.</summary>
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

        /// <summary>Mirrors ChangeQuestItem (S07_MyGame04.cpp:2223-2244): scans page 0 then page 1, replaces the FIRST matching slot's content via <paramref name="transform" />. False if not found anywhere.</summary>
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

        /// <summary>Splits the (at most 2) touched containers into the (Container, Items) parameter pairs <see cref="CharacterRepository.ApplyQuestTransitionAsync" /> expects -- null container = "not touched".</summary>
        public (byte? Container1, List<CharacterItemSlotTvp> Items1, byte? Container2, List<CharacterItemSlotTvp> Items2)
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

        private static readonly byte[] InventoryContainers = [ContainerMatrix.InventoryPage0, ContainerMatrix.InventoryPage1];
    }
}
