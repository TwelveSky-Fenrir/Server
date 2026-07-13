using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Quests;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Packets.Zone;
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

        if (!IsNearQuestAnchorNpc(zone, state, progress.StepPermanent + 1, false))
        {
            logger.LogInformation(
                "Character {CharacterId} quest-accept rejected: not within range of the step {Step} start NPC",
                characterId, progress.StepPermanent + 1);
            return new QuestActionResult(false);
        }

        if (result.DepositItemId is { } depositItemId)
        {
            if (!TryValidateDepositSlot(state, packet.Page1, packet.Index1, packet.XPost, packet.YPost, edits,
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

        if (!IsNearQuestAnchorNpc(zone, state, progress.StepPermanent, true))
        {
            logger.LogInformation(
                "Character {CharacterId} quest-complete rejected: not within range of the step {Step} end NPC",
                characterId, progress.StepPermanent);
            return new QuestActionResult(false);
        }

        var itemRewardDeclared = packet.Page1 != -1;

        if (itemRewardDeclared)
        {
            if (!TryValidateDepositSlot(state, packet.Page1, packet.Index1, packet.XPost, packet.YPost, edits,
                    out var container, out var slot))
                return new QuestActionResult(false);

            edits.Deposit(container, slot,
                new ItemStack(result.RewardItemId, result.RewardItemQuantity, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        if (result.DeleteItemId > 0)
            edits.DeleteFirstMatch(result.DeleteItemId);

        await PersistAndMirrorAsync(zone, characterId, result.NewProgress, result.MoneyReward,
            result.ExperienceReward, result.KillOtherTribeCountReward, result.TeacherPointReward, edits, ct);

        var hasNumericReward = result.MoneyReward != 0 || result.ExperienceReward != 0 ||
                               result.KillOtherTribeCountReward != 0 || result.TeacherPointReward != 0;

        if (itemRewardDeclared || hasNumericReward)
            await eventLog.LogAsync(QuestRewardEventCode, EventLogCategory.ItemCreate, accountId, characterId,
                null, null, null,
                result.MoneyReward != 0 ? result.MoneyReward : null, null,
                itemRewardDeclared ? result.RewardItemId : null,
                itemRewardDeclared ? result.RewardItemQuantity : null,
                1,
                hasNumericReward
                    ? $"ExperienceReward={result.ExperienceReward};KillOtherTribeCountReward={result.KillOtherTribeCountReward};TeacherPointReward={result.TeacherPointReward}"
                    : null,
                ct);

        logger.LogInformation(
            "Character {CharacterId} completed quest step {StepPermanent}: money {MoneyReward}, experience {ExperienceReward}, item {RewardItemId}x{RewardItemQuantity} declared={ItemRewardDeclared}",
            characterId, result.NewProgress.StepPermanent, result.MoneyReward, result.ExperienceReward,
            result.RewardItemId, result.RewardItemQuantity, itemRewardDeclared);

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

        if (!TryValidateDepositSlot(state, packet.Page1, packet.Index1, packet.XPost, packet.YPost, edits,
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

    private async ValueTask PersistAndMirrorAsync(Zone zone, int characterId, QuestProgress newProgress,
        long deltaMoney, int experienceDelta, int killOtherTribeCountDelta, int teacherPointDelta,
        ContainerEdits edits, CancellationToken ct)
    {
        var (container1, items1, container2, items2) = edits.ToTvpPairs();

        await characters.ApplyQuestTransitionAsync(characterId, newProgress.StepPermanent, newProgress.ActiveFlag,
            newProgress.QSort, newProgress.TargetPhase, newProgress.KillCounter, deltaMoney,
            container1, items1, container2, items2, ct);

        if (!await zone.PostQuestCommandAndWaitAsync(
                new QuestZoneCommand(characterId, newProgress, experienceDelta, killOtherTribeCountDelta,
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

    private static bool TryValidateDepositSlot(PlayerRuntimeState state, int page, int index, int xPost, int yPost,
        ContainerEdits edits, out byte container, out byte slot)
    {
        container = 0;
        slot = 0;

        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, index) ||
            xPost is < 0 or > 7 || yPost is < 0 or > 7)
            return false;

        if (page == ContainerMatrix.InventoryPage1 && state.InventoryDate < GameDate.Today())
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
