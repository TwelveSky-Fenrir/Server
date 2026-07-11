using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class LuckyTicketUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    ILogger<LuckyTicketUseItemHandler> logger) : IUseItemHandler
{
    private static readonly BoxRewardSpec PlaceholderSpec = BoxRewardSpec.Uniform(0, ImmutableArray<int>.Empty);

    public static IEnumerable<int> HandledItemIds { get; } = [1035, 1036, 1037];

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var ticketItemId = context.Item.ItemId;

        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        var today = GameDate.Today();
        var secondPageAccessible = state.InventoryDate >= today;

        var plan = LootBoxOpenResolver.OpenSingle(PlaceholderSpec, context.Page, context.Index, context.Item,
            page0, page1, ResolveRewardSort, Random.Shared, today,
            () => DrawReward(ticketItemId, state),
            secondPageAccessible,
            _ => LuckyTicketRewardResolver.ResolveFamilySerial(ticketItemId));

        switch (plan.Outcome)
        {
            case LootBoxOpenResolver.Outcome.RewardNotFound:
                logger.LogDebug(
                    "Character {CharacterId} op23 Lucky Ticket ({TicketId}) drew no eligible reward within budget: ticket kept",
                    context.CharacterId, ticketItemId);
                return UseItemResponses.Fail(context.Page, context.Index);

            case LootBoxOpenResolver.Outcome.InventoryFull:
                logger.LogDebug(
                    "Character {CharacterId} op23 Lucky Ticket ({TicketId}) reward {RewardId} could not be placed (inventory full): ticket kept",
                    context.CharacterId, ticketItemId, plan.RewardItemId);
                return UseItemResponses.InventoryFull(context.Page, context.Index);
        }

        await inventoryWriter.ReplaceProjectedPagesAndMirrorAsync(context.Zone, context.CharacterId, page0, page1,
            plan.ProjectedPage0, plan.ProjectedPage1, null, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} op23 Lucky Ticket ({TicketId}) opened: reward {RewardId} -> {Container}:{Slot} ({Placement})",
            context.CharacterId, ticketItemId, plan.RewardItemId, plan.RewardContainer, plan.RewardSlot,
            plan.PlacementOutcome);

        return UseItemResponses.Success(context.Page, context.Index);
    }

    private int DrawReward(int ticketItemId, PlayerRuntimeState state)
    {
        return LuckyTicketRewardResolver.TryDraw(worldData, Random.Shared, ticketItemId, state.PreviousTribe,
            state.Level, state.Level2, LuckyTicketRewardResolver.ShippedProductionEliteTierEnabled,
            out var rewardItemId)
            ? rewardItemId
            : 0;
    }

    private byte? ResolveRewardSort(int rewardItemId)
    {
        return worldData.ItemsById.TryGetValue(rewardItemId, out var def) ? def.Item.Sort : null;
    }
}
