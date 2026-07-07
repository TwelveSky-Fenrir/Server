using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Quests;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Quests;

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

        if (result.DepositItemId is { } depositItemId)
        {
            if (!TryValidateDepositSlot(packet.Page1, packet.Index1, packet.XPost, packet.YPost, edits,
                    out var container, out var slot))
                return new QuestActionResult(false);

            edits.Deposit(container, slot, new ItemStack(depositItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        await PersistAndMirrorAsync(zone, characterId, result.NewProgress, 0, 0, 0, 0, edits, ct);

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

        if (!edits.TryReplaceFirstMatch(result.FromItemId, _ =>
                new ItemStack(result.ToItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)))
            return new QuestActionResult(false);

        await PersistAndMirrorAsync(zone, characterId, result.NewProgress, 0, 0, 0, 0, edits, ct);

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
