using System.Collections.Frozen;
using System.Collections.Immutable;
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

    private const byte StackableSort = 2;

    private const byte MaterialSort = 99;

    private static readonly int[] PagePack = [1, 10, 100, 1000, 10000, 100000, 1000000, 10000000];

    private static readonly int[] IndexPack = [1, 100, 10000, 1000000, 1, 100, 10000, 1000000];

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
        var packedPage = 0;
        var packedIndex1 = 0;
        var packedIndex2 = 0;
        var packedXy1 = 0;
        var packedXy2 = 0;

        for (var i = 0; i < itemsToCreate.Length; i++)
        {
            var rewardId = itemsToCreate[i];

            var placement = InventoryFreeSlotFinder.FindInPages(projectedPage0, projectedPage1, worldData, rewardId,
                secondPageAccessible);

            if (placement is not { } cell)
            {
                logger.LogDebug(
                    "Character {CharacterId} multi-item-create item {ItemId}: inventory full after placing {Placed}/{Total} items -- source kept",
                    context.CharacterId, itemId, i, itemsToCreate.Length);
                return UseItemResponses.InventoryFull(context.Page, context.Index);
            }

            var sort = worldData.ItemsById.TryGetValue(rewardId, out var def) ? def.Item.Sort : (byte)0;
            var stack = new ItemStack(rewardId, ResolveCreatedQuantity(rewardId, sort), 0, 0, 0, 0, 0, 0, 0, 0, 0,
                cell.X, cell.Y);

            if (cell.Container == ContainerMatrix.InventoryPage0)
                projectedPage0 = projectedPage0.SetItem(cell.Slot, stack);
            else
                projectedPage1 = projectedPage1.SetItem(cell.Slot, stack);

            if (i >= ResponseSlots)
                continue;

            placedItemIds[i] = rewardId;
            packedPage += PagePack[i] * cell.Container;
            if (i < 4)
            {
                packedIndex1 += IndexPack[i] * cell.Slot;
                packedXy1 += IndexPack[i] * cell.GridIndex;
            }
            else
            {
                packedIndex2 += IndexPack[i] * cell.Slot;
                packedXy2 += IndexPack[i] * cell.GridIndex;
            }
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
            Page = packedPage,
            Index1 = packedIndex1,
            Index2 = packedIndex2,
            Xy1 = packedXy1,
            Xy2 = packedXy2,
            ItemIndex = placedItemIds,
            Value = [0, 0, 0, 0]
        });

        logger.LogInformation(
            "Character {CharacterId} multi-item-create item {ItemId}: created {Count} item(s) (Num={Num})",
            context.CharacterId, itemId, count, num);

        return UseItemResponses.Success(context.Page, context.Index);
    }

    private static int ResolveCreatedQuantity(int rewardItemId, byte sort)
    {
        return sort switch
        {
            StackableSort => rewardItemId is 1109 or 1224 ? 12 : 99,
            MaterialSort => 1,
            _ => 0
        };
    }
}
