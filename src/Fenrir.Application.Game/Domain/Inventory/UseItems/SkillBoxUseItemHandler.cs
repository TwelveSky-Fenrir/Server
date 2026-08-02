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

/// <summary>
///     Handles item 864 (Skill Box) and item 889 (TEST Skill Box) — tribe-indexed skill-book loot boxes.
/// </summary>
/// <remarks>
///     Legacy reference: Server/ts25zone/S04_MyWork03.cpp:4300-4333 — <c>WUSE_ITEM_864</c> / <c>WUSE_ITEM_889</c>
///     blocks. Both items share the identical body.
///     <para>
///         The reward is chosen by <c>rand_mir() % 20</c>. Roll 0 produces a tribe-specific skill book:
///         base ID 90317 + (rand%6) + (previousTribe × 6), yielding items 90317-90322 for tribe 0,
///         90323-90328 for tribe 1, and 90329-90334 for tribe 2.
///         Rolls 1-5 and 8 map to fixed skill-book IDs.
///         Rolls 6 and 7 both yield item 90541: roll 6 falls through to roll 7 in the legacy
///         C++ switch (a bug), so the effective weight for 90541 is 2/20.
///         Rolls 9-19 hit the default branch and yield item 90568 (11/20 weight).
///     </para>
/// </remarks>
public sealed class SkillBoxUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    ILogger<SkillBoxUseItemHandler> logger) : IUseItemHandler
{
    // Dummy spec: reward is always determined by the override delegate below, not by the spec table.
    private static readonly BoxRewardSpec RewardDrawnDynamicallySpec =
        BoxRewardSpec.Uniform(0, ImmutableArray<int>.Empty);

    /// <summary>Item IDs handled by this handler: 864 (Skill Box) and 889 (TEST Skill Box).</summary>
    /// <remarks>
    ///     Item 889 shares the identical handler body per
    ///     Server/ts25zone/S04_MyWork03.cpp:4333 — the TEST item's case simply falls through to 864's block.
    /// </remarks>
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

    /// <summary>
    ///     Rolls the reward item ID using the legacy <c>rand_mir() % 20</c> distribution.
    /// </summary>
    /// <remarks>
    ///     Réf. Server/ts25zone/S04_MyWork03.cpp:4300-4333.
    ///     Case 6 falls through to case 7 in the original switch, doubling item 90541's probability (2/20).
    ///     Cases 9-19 share the default branch, yielding item 90568 (11/20 total weight).
    /// </remarks>
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
            6 or 7 => 90541,   // legacy fall-through: case 6 drops to case 7, both yield 90541 (2/20 weight)
            8 => 90542,
            _ => 90568          // cases 9-19 hit default (11/20 weight)
        };
    }

    private byte? ResolveRewardSort(int rewardItemId)
    {
        return worldData.ItemsById.TryGetValue(rewardItemId, out var def) ? def.Item.Sort : null;
    }
}
