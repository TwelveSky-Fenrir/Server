using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

public sealed class LootBoxUseItemHandler(
    WorldDataCache worldData,
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    ILogger<LootBoxUseItemHandler> logger) : IUseItemHandler
{
    private const short BoxOpenBeforeEventCode = 5;

    private const short BoxOpenRewardEventCode = 6;

    private const byte SuccessOutcome = 1;

    private const int WingLuckyBoxId = 8005;

    private const int LoyKrathongBoxId = 8108;

    private const int NoRewardItemId = 0;


    private static readonly BoxRewardSpec CostumeChestOverrideSpec = BoxRewardSpec.Uniform(
        CostumeChest76543RewardTable.BoxItemId, ImmutableArray<int>.Empty, CostumeChest76543RewardTable.RentalDays);

    private static readonly BoxRewardSpec SkyWarlordChestOverrideSpec =
        BoxRewardSpec.Uniform(WarlordChestRewardTable.SkyChestBoxItemId, ImmutableArray<int>.Empty);

    private static readonly BoxRewardSpec EarthWarlordChestOverrideSpec =
        BoxRewardSpec.Uniform(WarlordChestRewardTable.EarthChestBoxItemId, ImmutableArray<int>.Empty);

    private static readonly BoxRewardSpec HeavenlyJadeChestOverrideSpec =
        BoxRewardSpec.Uniform(HeavenlyJadeChest1236RewardTable.BoxId, ImmutableArray<int>.Empty);

    private static readonly FrozenDictionary<byte, BoxRewardSpec> WingLuckyBoxSpecByPreviousTribe =
        new Dictionary<byte, BoxRewardSpec>
        {
            [0] = BuildWingLuckyBoxSpec(201),
            [1] = BuildWingLuckyBoxSpec(202),
            [2] = BuildWingLuckyBoxSpec(203)
        }.ToFrozenDictionary();

    private static readonly BoxRewardSpec LoyKrathongBoxSpec = BoxRewardSpec.Weighted(LoyKrathongBoxId,
    [
        new LootBoxRewardResolver.WeightedReward(1407, 6),
        new LootBoxRewardResolver.WeightedReward(1103, 8),
        new LootBoxRewardResolver.WeightedReward(2397, 12),
        new LootBoxRewardResolver.WeightedReward(698, 20),
        new LootBoxRewardResolver.WeightedReward(1221, 22),
        new LootBoxRewardResolver.WeightedReward(NoRewardItemId, 32)
    ]);

    private static readonly BoxRewardSpec ChestBox720OverrideSpec =
        BoxRewardSpec.Uniform(ChestBox720RewardTable.BoxId, ImmutableArray<int>.Empty);

    public static ImmutableArray<int> HandledItemIds { get; } = LootBoxCatalog.Default.RegisteredBoxIds
        .Add(CostumeChest76543RewardTable.BoxItemId)
        .Add(WarlordChestRewardTable.SkyChestBoxItemId)
        .Add(WarlordChestRewardTable.EarthChestBoxItemId)
        .Add(HeavenlyJadeChest1236RewardTable.BoxId)
        .Add(WingLuckyBoxId)
        .Add(LoyKrathongBoxId)
        .Add(ChestBox720RewardTable.BoxId)
        .Add(512)
        .Add(664)
        .Add(1043);

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var boxId = context.Item.ItemId;

        if (ResolveSpec(boxId, context.State.PreviousTribe) is not { } spec || context.Item.Quantity < 1)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 loot-box ({BoxId}) rejected: no populated reward table or empty stack (quantity {Quantity})",
                context.CharacterId, boxId, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var isBulk = LootBoxCatalog.BulkOpenWhitelist.Contains(boxId) && context.Value > 1;
        return isBulk
            ? await OpenBulkAsync(context, spec, cancellationToken)
            : await OpenSingleAsync(context, spec, cancellationToken);
    }

    private static BoxRewardSpec BuildWingLuckyBoxSpec(int tribeWingItemId)
    {
        return BoxRewardSpec.RareBandThenPools(WingLuckyBoxId,
            ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty,
            [
                new LootBoxRewardResolver.RewardPool(5, [2477]),
                new LootBoxRewardResolver.RewardPool(60, [tribeWingItemId]),
                new LootBoxRewardResolver.RewardPool(100, [2397, 694, 693, 692, 696, 698]),
                new LootBoxRewardResolver.RewardPool(180, [506, 507, 508, 509, 578, 579]),
                new LootBoxRewardResolver.RewardPool(199, [1166, 1118, 1103, 1222, 1145, 1237])
            ]);
    }

    private static BoxRewardSpec? ResolveSpec(int boxId, byte previousTribe)
    {
        if (LootBoxCatalog.Default.TryGetSpec(boxId) is { } spec)
            return spec;

        if (boxId == WingLuckyBoxId)
            return WingLuckyBoxSpecByPreviousTribe.TryGetValue(previousTribe, out var wingSpec) ? wingSpec : null;

        return boxId switch
        {
            CostumeChest76543RewardTable.BoxItemId => CostumeChestOverrideSpec,
            WarlordChestRewardTable.SkyChestBoxItemId => SkyWarlordChestOverrideSpec,
            WarlordChestRewardTable.EarthChestBoxItemId => EarthWarlordChestOverrideSpec,
            HeavenlyJadeChest1236RewardTable.BoxId => HeavenlyJadeChestOverrideSpec,
            LoyKrathongBoxId => LoyKrathongBoxSpec,
            ChestBox720RewardTable.BoxId => ChestBox720OverrideSpec,
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> OpenSingleAsync(UseItemContext context, BoxRewardSpec spec,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        var today = GameDate.Today();
        var plan = LootBoxOpenResolver.OpenSingle(spec, context.Page, context.Index, context.Item, page0, page1,
            ResolveRewardSort, Random.Shared, today, ResolveRewardIdOverride(context, spec),
            state.InventoryDate >= today);

        await MirrorM15PetLuckyBoxPityAsync(context, cancellationToken);

        switch (plan.Outcome)
        {
            case LootBoxOpenResolver.Outcome.RewardNotFound:
                logger.LogDebug(
                    "Character {CharacterId} op23 loot-box ({BoxId}) rolled unknown reward {RewardId}: box kept",
                    context.CharacterId, context.Item.ItemId, plan.RewardItemId);
                return UseItemResponses.Fail(context.Page, context.Index);

            case LootBoxOpenResolver.Outcome.InventoryFull:
                logger.LogDebug(
                    "Character {CharacterId} op23 loot-box ({BoxId}) reward {RewardId} could not be placed (inventory full): box kept",
                    context.CharacterId, context.Item.ItemId, plan.RewardItemId);
                return UseItemResponses.InventoryFull(context.Page, context.Index);
        }

        await LogBoxOpenBeforeAsync(context, cancellationToken);

        await PersistAndMirrorAsync(context, page0, page1, plan.ProjectedPage0, plan.ProjectedPage1,
            cancellationToken);

        await LogBoxRewardAsync(context, plan.RewardItemId, plan.RewardQuantity, plan.RewardContainer,
            plan.RewardSlot, cancellationToken);

        await AttemptNoticeAsync(context, plan.RewardItemId, plan.RewardQuantity, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} op23 loot-box ({BoxId}) opened: reward {RewardId} x{Quantity} -> {Container}:{Slot} ({Placement}), box remaining {Remaining}",
            context.CharacterId, context.Item.ItemId, plan.RewardItemId, plan.RewardQuantity, plan.RewardContainer,
            plan.RewardSlot, plan.PlacementOutcome, plan.BoxRemainingQuantity);

        return UseItemResponses.Success(context.Page, context.Index);
    }

    private async ValueTask<UseInventoryItemResponse> OpenBulkAsync(UseItemContext context, BoxRewardSpec spec,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        var today = GameDate.Today();
        var plan = LootBoxOpenResolver.OpenBulk(spec, context.Page, context.Index, context.Item, page0, page1,
            ResolveRewardSort, Random.Shared, today, context.Value, ResolveRewardIdOverride(context, spec),
            state.InventoryDate >= today);

        await MirrorM15PetLuckyBoxPityAsync(context, cancellationToken);

        if (plan.OpenedCount == 0)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 loot-box bulk ({BoxId}) opened nothing (inventory full or empty stack): box kept",
                context.CharacterId, context.Item.ItemId);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        await LogBoxOpenBeforeAsync(context, cancellationToken);

        await PersistAndMirrorAsync(context, page0, page1, plan.ProjectedPage0, plan.ProjectedPage1,
            cancellationToken);

        foreach (var reward in plan.Rewards)
        {
            await LogBoxRewardAsync(context, reward.RewardItemId, reward.RewardQuantity, reward.RewardContainer,
                reward.RewardSlot, cancellationToken);
            await AttemptNoticeAsync(context, reward.RewardItemId, reward.RewardQuantity, cancellationToken);
        }

        logger.LogInformation(
            "Character {CharacterId} op23 loot-box bulk ({BoxId}) opened {Opened} box(es), box remaining {Remaining}",
            context.CharacterId, context.Item.ItemId, plan.OpenedCount, plan.BoxRemainingQuantity);

        return UseItemResponses.Success(context.Page, context.Index);
    }

    private async ValueTask PersistAndMirrorAsync(UseItemContext context,
        ImmutableDictionary<byte, ItemStack> originalPage0, ImmutableDictionary<byte, ItemStack> originalPage1,
        ImmutableDictionary<byte, ItemStack> projectedPage0, ImmutableDictionary<byte, ItemStack> projectedPage1,
        CancellationToken cancellationToken)
    {
        var page0Changed = !ReferenceEquals(projectedPage0, originalPage0);
        var page1Changed = !ReferenceEquals(projectedPage1, originalPage1);

        ImmutableArray<InventoryContainerSnapshot> containers;
        if (page0Changed && page1Changed)
        {
            await characters.ReplaceTwoContainersAsync(context.CharacterId,
                ContainerMatrix.InventoryPage0, ToTvps(projectedPage0),
                ContainerMatrix.InventoryPage1, ToTvps(projectedPage1), cancellationToken);
            containers = ImmutableArray.Create(
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, projectedPage0),
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage1, projectedPage1));
        }
        else
        {
            var container = page1Changed ? ContainerMatrix.InventoryPage1 : ContainerMatrix.InventoryPage0;
            var projected = page1Changed ? projectedPage1 : projectedPage0;
            await characters.ReplaceContainerAsync(context.CharacterId, container, ToTvps(projected),
                cancellationToken);
            containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projected));
        }

        if (!await context.Zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(context.CharacterId, containers, null), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped op23 loot-box mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                context.Zone.MapId, context.CharacterId);
    }

    private async ValueTask AttemptNoticeAsync(UseItemContext context, int rewardItemId, int rewardQuantity,
        CancellationToken cancellationToken)
    {
        var rewardType = worldData.ItemsById.TryGetValue(rewardItemId, out var def) ? def.Item.Type : (byte)0;
        var decision = NoticeForBoxResolver.Decide(context.Item.ItemId, rewardItemId, rewardType);

        if (decision.WriteEliteGainAudit)
            await eventLog.LogBoxOpenEliteGainAsync(context.AccountId, context.CharacterId, context.Item.ItemId,
                rewardItemId, rewardQuantity, cancellationToken);

        if (decision.ShouldBroadcast)
            logger.LogInformation(
                "Loot-box elite notice (legacy NoticeForBox center relay, not client-broadcast -- deferred, see LootBoxUseItemHandler remarks): character {CharacterId} box {BoxId} reward {RewardId}",
                context.CharacterId, context.Item.ItemId, rewardItemId);
    }

    private byte? ResolveRewardSort(int rewardItemId)
    {
        return worldData.ItemsById.TryGetValue(rewardItemId, out var def) ? def.Item.Sort : null;
    }

    private static Func<int>? ResolveRewardIdOverride(UseItemContext context, BoxRewardSpec spec)
    {
        var boxId = context.Item.ItemId;

        if (boxId == CloakBoxRewardTable.BoxId)
            return () =>
            {
                var result = CloakBoxRewardTable.Roll(context.State.CloakLuckyBoxPity, Random.Shared);
                context.State.CloakLuckyBoxPity = result.NewPityCounter;
                return result.RewardItemId;
            };

        if (boxId == CostumeChest76543RewardTable.BoxItemId)
        {
            var previousTribe = context.State.PreviousTribe;
            return () =>
            {
                var result = CostumeChest76543RewardTable.Roll(previousTribe, Random.Shared);
                return result.Success ? result.RewardItemId : 0;
            };
        }

        if (boxId is WarlordChestRewardTable.SkyChestBoxItemId or WarlordChestRewardTable.EarthChestBoxItemId)
        {
            var previousTribe = context.State.PreviousTribe;
            var level2 = context.State.Level2;
            return () =>
            {
                if (!WarlordChestRewardTable.MeetsLevelGate(level2))
                    return 0;

                return WarlordChestRewardTable.TryRollReward(boxId, previousTribe, Random.Shared, out var rewardId)
                    ? rewardId
                    : 0;
            };
        }

        if (boxId == M15PetLuckyBox8111RewardTable.BoxId)
            return () =>
            {
                var result = M15PetLuckyBox8111RewardTable.Roll(context.State.M15PetLuckyBoxPity, Random.Shared);
                context.State.M15PetLuckyBoxPity = result.NewPityCounter;
                return result.RewardItemId;
            };

        if (boxId == CloakVariantBox8114RewardTable.BoxId)
            return () =>
            {
                var result = CloakVariantBox8114RewardTable.Roll(context.State.CloakVariantBoxPity, Random.Shared);
                context.State.CloakVariantBoxPity = result.NewPityCounter;
                return result.RewardItemId;
            };

        if (boxId == MountVariantBox8115RewardTable.BoxId)
            return () =>
            {
                var result = MountVariantBox8115RewardTable.Roll(context.State.MountVariantBoxPity, Random.Shared);
                context.State.MountVariantBoxPity = result.NewPityCounter;
                return result.RewardItemId;
            };

        if (boxId == HeavenlyJadeChest1236RewardTable.BoxId)
        {
            var previousTribe = context.State.PreviousTribe;
            return () =>
            {
                var result = HeavenlyJadeChest1236RewardTable.Roll(previousTribe, Random.Shared);
                return result.Success ? result.RewardItemId : 0;
            };
        }

        if (boxId == WingLuckyBoxId)
            return () => LootBoxRewardResolver.RollPools(Random.Shared, spec.Pools);

        if (boxId == ChestBox720RewardTable.BoxId)
        {
            var previousTribe = context.State.PreviousTribe;
            return () =>
            {
                var result = ChestBox720RewardTable.Roll(previousTribe, Random.Shared);
                return result.Success ? result.RewardItemId : 0;
            };
        }

        return null;
    }

    private async ValueTask MirrorM15PetLuckyBoxPityAsync(UseItemContext context, CancellationToken cancellationToken)
    {
        if (context.Item.ItemId != M15PetLuckyBox8111RewardTable.BoxId)
            return;

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId,
                    M15PetLuckyBoxPity: context.State.M15PetLuckyBoxPity),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped M15 Pet Lucky Box (8111) pity-counter persistence mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);
    }

    private ValueTask LogBoxOpenBeforeAsync(UseItemContext context, CancellationToken cancellationToken)
    {
        return eventLog.LogAsync(BoxOpenBeforeEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"Page={context.Page};Index={context.Index};Serial={context.Item.Serial}",
            cancellationToken);
    }

    private ValueTask LogBoxRewardAsync(UseItemContext context, int rewardItemId, int rewardQuantity,
        byte rewardContainer, byte rewardSlot, CancellationToken cancellationToken)
    {
        return eventLog.LogAsync(BoxOpenRewardEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, rewardItemId, rewardQuantity, SuccessOutcome,
            $"Box={context.Item.ItemId};Reward={rewardItemId}x{rewardQuantity};Slot={rewardContainer}:{rewardSlot}",
            cancellationToken);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
