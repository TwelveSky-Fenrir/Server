using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

/// <summary>
///     Handles items that create a fixed set of multiple items simultaneously and notify the client
///     via <c>B_MULTI_ITEM_CREATE_RECV</c> (opcode 119, <see cref="MultiItemCreateResponse" />).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:4671-4848.
///     The items handled, their output lists, and their <c>Num</c> encoding:
///     <list type="bullet">
///         <item>
///             <term>835</term>
///             <description>Creates 8× item 1437 (upgrade stones). Num = 6008 (6000 + 8).</description>
///         </item>
///         <item>
///             <term>8006</term>
///             <description>Creates 3 items: 2138 (premium 30d), 1215 (auto-buff 30d), 1217 (auto-hunt 30d).
///             Num = 6003 (6000 + 3).</description>
///         </item>
///         <item>
///             <term>99102–99108</term>
///             <description>Creates N× item 1437 where N = (itemId − 99100). Num = 8000 + N.</description>
///         </item>
///         <item>
///             <term>2311</term>
///             <description>Creates 8 items: 1437, 1437, 1103, 1103, 1126, 1126, 2397, 2397.
///             Num = 6008 (6000 + 8).</description>
///         </item>
///     </list>
///     The <c>Num</c> encoding: 6000 + count for standard multi-creates; 8000 + count for the 99xxx series.
///     The source item is consumed (removed) as part of the same atomic inventory write that places the
///     created items, so no partial state is ever left behind.
///     On inventory-full, the source item is kept and no items are placed.
/// </remarks>
public sealed class MultiItemCreateUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    ILogger<MultiItemCreateUseItemHandler> logger) : IUseItemHandler
{
    // Fixed array size on the wire packet.
    private const int ResponseSlots = 8;

    // Num = base + item count (legacy encoding).
    private const int NumBaseStandard = 6000;
    private const int NumBase99Xxx = 8000;

    /// <summary>
    ///     Fixed output-item lists keyed by the source item ID.
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:4671-4848.
    /// </summary>
    private static readonly FrozenDictionary<int, int[]> ItemListById = new Dictionary<int, int[]>
    {
        // Item 835 → 8× upgrade stones (1437). Réf. S04_MyWork03.cpp:~4671.
        [835] = [1437, 1437, 1437, 1437, 1437, 1437, 1437, 1437],

        // Item 8006 → premium 30d + auto-buff 30d + auto-hunt 30d. Réf. S04_MyWork03.cpp:~4700.
        [8006] = [2138, 1215, 1217],

        // Item 2311 → mixed 8-item grant. Réf. S04_MyWork03.cpp:~4800.
        [2311] = [1437, 1437, 1103, 1103, 1126, 1126, 2397, 2397],

        // Items 99102–99108: N = (itemId − 99100) upgrade stones. Réf. S04_MyWork03.cpp:~4730.
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

        // Build projected inventory with the source item removed first, so its slot is available
        // for new items and doesn't confuse the merge-slot search in the placement resolver.
        var projectedPage0 = context.Page == ContainerMatrix.InventoryPage0
            ? page0.Remove(context.Index)
            : page0;
        var projectedPage1 = context.Page == ContainerMatrix.InventoryPage1
            ? page1.Remove(context.Index)
            : page1;

        // Greedily place each created item into the projected state. If any item cannot be placed
        // (inventory full), abort without touching persistence -- the source item stays.
        var placedItemIds = new int[ResponseSlots]; // zero-padded to 8 for the wire packet
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

        // Persist the source-item removal + all created items in one atomic inventory write.
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
