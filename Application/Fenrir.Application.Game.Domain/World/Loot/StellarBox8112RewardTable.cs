using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Reward-table DATA for item 8112 (Stellar Core Lucky Box) -- workstream C10-stellarbox8112. A sibling
///     data file to <see cref="LootBoxCatalog" />, not an edit to it: the box-open mechanism (single open, bulk
///     open, quantity clamp, placement, double-GL_606 audit, notice decision) already exists and is fully
///     generic over any <see cref="BoxRewardSpec" /> the catalog registers (<see cref="LootBoxOpenResolver" />,
///     <see cref="LootBoxUseItemHandler" />). This file supplies only the missing
///     per-grade roll-band DATA; plugging <see cref="Spec" /> into <see cref="LootBoxCatalog" />'s registered
///     box table is a one-line addition left to the serial integration pass (see this workstream's wiring
///     manifest) since concurrent sibling waves may also be touching that same constructor's list.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:1673-1695 (bulk-path per-box worker, <c>OpenSingleBoxNoStellar</c>)
///     and :7788-7821 (single-path <c>case 8112</c> inside the request handler's own per-item switch) -- both
///     sites carry byte-identical seven-band cutoffs and reward ids, confirmed by direct side-by-side read.
///     <para>
///         Modeled as <see cref="BoxRewardKind.Weighted" />: the legacy table is seven contiguous, exhaustive
///         roll bands over an inclusive 0..999 draw (widths 350/200/150/120/90/60/30, summing to exactly 1000,
///         so the final band's "otherwise" clause is never a true fallback). This is exactly the shape
///         <see cref="LootBoxRewardResolver.RollWeighted" /> already implements -- cumulative
///         <see cref="LootBoxRewardResolver.WeightedReward.Weight" /> spans checked against
///         <c>random.Next(0, totalWeight)</c> -- the same primitive box 7105 already uses in
///         <see cref="LootBoxCatalog" />, reused here rather than inventing a new roll shape for a table that is
///         structurally identical (a "well-distributed 31-bit generator reduced modulo 1000" roll per the C10
///         contract's own Inputs section -- <c>rand_mir</c>/<c>gjrand_rand</c>, Server/Header/random.h:4-9,
///         Server/Header/random.cpp:50-70 -- which this codebase's convention already abstracts behind
///         <see cref="System.Random" />/<c>Random.Shared</c> for every other box, not reproduced bit-for-bit).
///     </para>
///     <para>
///         <see cref="BoxRewardSpec.RentalDays" /> is deliberately left at its default (0): the C10 contract's
///         side effect 7 states the reward's expiry field is left untouched for every one of the seven grades
///         (none of 93500-93506 belong to the fixed <c>IsRentItem</c> id list, Server/Header/function.h:2216-2265),
///         so no rental is ever stamped for this box regardless of the rolled reward's stackability.
///     </para>
///     <para>
///         Reward quantity (contract side effect 5, "quantity written as zero, not one") and expiry-suppression
///         (side effect 7) both fall out of the existing, already-tested
///         <see cref="BoxRewardPlacementResolver.ResolveQuantity" />/<see cref="LootBoxOpenResolver.OpenSingle" />
///         machinery with zero changes needed here: all seven reward ids (93500-93506) carry catalog "sort"
///         value 3 (Server/BuildEU33/ITEM_DUMP_CLEAN.csv:34308-34314), which is neither the stackable-materials
///         sort (2/99) nor the pet sort (22) that machinery special-cases, so it already falls to the
///         zero-quantity default -- no reward-shape override is needed for this box.
///     </para>
///     <para>
///         Deliberately NOT modeled here (contract-flagged open questions/out-of-scope items, never invented):
///         the mini-boss (monster id 746) independent 30% direct-drop path for item 93500
///         (Server/ts25zone/S07_MyGame05.cpp:2586-2628) and the zone-38 GM-toggleable PvP-kill unopened-box-drop
///         event that can grant an unopened 8112 (Server/ts25zone/S07_MyGame03.cpp:3876-3919) are both separate
///         acquisition paths with their own odds, not part of this box's own roll table, and are out of scope for
///         this data file. Whether the five downstream stat-lookup formulas these seven grades drive
///         (Server/Header/Protocol/MyFactor.cpp:2145-2162,4643-4825) are actually wired into live combat
///         resolution was not checked by the C10-stellarbox8112 contract and is not addressed here either --
///         flagged for a dedicated combat-pipeline confirmation pass, not this data file's concern.
///     </para>
/// </remarks>
public static class StellarBox8112RewardTable
{
    /// <summary>
    ///     The seven Stellar Core grades this box can roll, in ascending roll-band order (each band's width is
    ///     its <see cref="LootBoxRewardResolver.WeightedReward.Weight" />): "FF" 0-349 (35.0%) -&gt; 93500,
    ///     "E" 350-549 (20.0%) -&gt; 93501, "EE" 550-699 (15.0%) -&gt; 93502, "D" 700-819 (12.0%) -&gt; 93503,
    ///     "DD" 820-909 (9.0%) -&gt; 93504, "C" 910-969 (6.0%) -&gt; 93505, "CC" 970-999 (3.0%) -&gt; 93506.
    /// </summary>
    public static readonly BoxRewardSpec Spec = BoxRewardSpec.Weighted(8112,
    [
        new LootBoxRewardResolver.WeightedReward(93500, 350),
        new LootBoxRewardResolver.WeightedReward(93501, 200),
        new LootBoxRewardResolver.WeightedReward(93502, 150),
        new LootBoxRewardResolver.WeightedReward(93503, 120),
        new LootBoxRewardResolver.WeightedReward(93504, 90),
        new LootBoxRewardResolver.WeightedReward(93505, 60),
        new LootBoxRewardResolver.WeightedReward(93506, 30)
    ]);
}
