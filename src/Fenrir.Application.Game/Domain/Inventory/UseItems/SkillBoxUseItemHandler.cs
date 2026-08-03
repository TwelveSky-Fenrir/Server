using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class SkillBoxUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    ILogger<SkillBoxUseItemHandler> logger) : IUseItemHandler
{
    private static readonly BoxRewardSpec RewardDrawnDynamicallySpec =
        BoxRewardSpec.Uniform(0, ImmutableArray<int>.Empty);

        public static IEnumerable<int> HandledItemIds { get; } = [864, 889];

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;

        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        var today = GameDate.Today();
        var secondPageAccessible = state.InventoryDate >= today;

        var plan = LootBoxOpenResolver.OpenSingle(
            RewardDrawnDynamicallySpec,
            context.Page, context.Index, context.Item,
            page0, page1,
            ResolveRewardSort,
            Random.Shared,
            today,
            () => RollReward(state.PreviousTribe),
            secondPageAccessible);

        switch (plan.Outcome)
        {
            case LootBoxOpenResolver.Outcome.RewardNotFound:
                logger.LogDebug(
                    "Character {CharacterId} op23 Skill Box ({BoxId}) drew unrecognised reward {RewardId}: box kept",
                    context.CharacterId, context.Item.ItemId, plan.RewardItemId);
                return UseItemResponses.Fail(context.Page, context.Index);

            case LootBoxOpenResolver.Outcome.InventoryFull:
                logger.LogDebug(
                    "Character {CharacterId} op23 Skill Box ({BoxId}) reward {RewardId} could not be placed (inventory full): box kept",
                    context.CharacterId, context.Item.ItemId, plan.RewardItemId);
                return UseItemResponses.InventoryFull(context.Page, context.Index);
        }

        await inventoryWriter.ReplaceProjectedPagesAndMirrorAsync(context.Zone, context.CharacterId, page0, page1,
            plan.ProjectedPage0, plan.ProjectedPage1, null, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} op23 Skill Box ({BoxId}) opened: reward {RewardId} -> {Container}:{Slot} ({Placement})",
            context.CharacterId, context.Item.ItemId, plan.RewardItemId, plan.RewardContainer, plan.RewardSlot,
            plan.PlacementOutcome);

        return UseItemResponses.Success(context.Page, context.Index);
    }

        private static int RollReward(byte previousTribe)
    {
        return Random.Shared.Next(20) switch
        {
            0 => 90317 + Random.Shared.Next(6) + previousTribe * 6,
            1 => 90567,
            2 => 90569,
            3 => 91299,
            4 => 91300,
            5 => 91323,
            6 or 7 => 90541,
            8 => 90542,
            _ => 90568
        };
    }

    private byte? ResolveRewardSort(int rewardItemId)
    {
        return worldData.ItemsById.TryGetValue(rewardItemId, out var def) ? def.Item.Sort : null;
    }
}
