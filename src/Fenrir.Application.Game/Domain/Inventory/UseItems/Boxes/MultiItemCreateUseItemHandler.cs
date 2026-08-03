using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

public sealed class MultiItemCreateUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    ILogger<MultiItemCreateUseItemHandler> logger) : IUseItemHandler
{
    private const int ResponseSlots = 8;

    private const int NumBaseStandard = 6000;
    private const int NumBase99Xxx = 8000;

        private static readonly FrozenDictionary<int, int[]> ItemListById = new Dictionary<int, int[]>
    {
        [835] = [1437, 1437, 1437, 1437, 1437, 1437, 1437, 1437],

        [8006] = [2138, 1215, 1217],

        [2311] = [1437, 1437, 1103, 1103, 1126, 1126, 2397, 2397],

        [99102] = [1437, 1437],
        [99103] = [1437, 1437, 1437],
        [99104] = [1437, 1437, 1437, 1437],
        [99105] = [1437, 1437, 1437, 1437, 1437],
        [99106] = [1437, 1437, 1437, 1437, 1437, 1437],
        [99107] = [1437, 1437, 1437, 1437, 1437, 1437, 1437],
        [99108] = [1437, 1437, 1437, 1437, 1437, 1437, 1437, 1437]
    }.ToFrozenDictionary();

    public static ImmutableArray<int> HandledItemIds { get; } =
    [
        835, 8006, 2311,
        99102, 99103, 99104, 99105, 99106, 99107, 99108
    ];

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var itemId = context.Item.ItemId;
        if (!ItemListById.TryGetValue(itemId, out var itemsToCreate))
        {
            logger.LogWarning(
                "Character {CharacterId} multi-item-create item {ItemId}: no output list configured -- rejecting",
                context.CharacterId, itemId);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var state = context.State;
        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);
        var secondPageAccessible = state.InventoryDate >= GameDate.Today();

        var projectedPage0 = context.Page == ContainerMatrix.InventoryPage0
            ? page0.Remove(context.Index)
            : page0;
        var projectedPage1 = context.Page == ContainerMatrix.InventoryPage1
            ? page1.Remove(context.Index)
            : page1;

        var placedItemIds = new int[ResponseSlots];
        for (var i = 0; i < itemsToCreate.Length; i++)
        {
            var rewardId = itemsToCreate[i];
            var sort = worldData.ItemsById.TryGetValue(rewardId, out var def) ? def.Item.Sort : (byte)0;
            var quantity = BoxRewardPlacementResolver.ResolveQuantity(sort, 1);
            var reward = new BoxRewardPlacementResolver.ResolvedReward(rewardId, quantity.Quantity,
                quantity.IsStackable, 0, 0, 0, 0, 0);

            var placement = BoxRewardPlacementResolver.Resolve(
                reward, context.Page, context.Index, projectedPage0, projectedPage1, secondPageAccessible);

            if (!placement.Succeeded)
            {
                logger.LogDebug(
                    "Character {CharacterId} multi-item-create item {ItemId}: inventory full after placing {Placed}/{Total} items -- source kept",
                    context.CharacterId, itemId, i, itemsToCreate.Length);
                return UseItemResponses.InventoryFull(context.Page, context.Index);
            }

            if (placement.Container == ContainerMatrix.InventoryPage0)
                projectedPage0 = projectedPage0.SetItem(placement.Slot, placement.NewStack!.Value);
            else
                projectedPage1 = projectedPage1.SetItem(placement.Slot, placement.NewStack!.Value);

            if (i < ResponseSlots)
                placedItemIds[i] = rewardId;
        }

        await inventoryWriter.ReplaceProjectedPagesAndMirrorAsync(
            context.Zone, context.CharacterId, page0, page1, projectedPage0, projectedPage1,
            null, cancellationToken);

        var isNinetyNineSeries = itemId is >= 99102 and <= 99108;
        var count = itemsToCreate.Length;
        var num = isNinetyNineSeries ? NumBase99Xxx + count : NumBaseStandard + count;

        context.Session.Send(new MultiItemCreateResponse
        {
            Num = num,
            Page = context.Page,
            Index1 = context.Index,
            Index2 = context.Index,
            Xy1 = 0,
            Xy2 = 0,
            ItemIndex = placedItemIds,
            Value = [0, 0, 0, 0]
        });

        logger.LogInformation(
            "Character {CharacterId} multi-item-create item {ItemId}: created {Count} item(s) (Num={Num})",
            context.CharacterId, itemId, count, num);

        return UseItemResponses.Success(context.Page, context.Index);
    }
}
