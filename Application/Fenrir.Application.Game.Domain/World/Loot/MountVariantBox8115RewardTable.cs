using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Item 8115 (in-code comment "15% Mount Box" -- workstream C10-remaining-box-pools) reward-pool + pity
///     data. Single-open only, no bulk-path counterpart exists (confirmed absent from
///     <see cref="LootBoxCatalog.BulkOpenWhitelist" />). Uses the already-reserved per-avatar pity counter
///     <see cref="World.PlayerRuntimeState.MountVariantBoxPity" /> (ceiling 200).
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ (C10-remaining-box-pools contract) : Server/ts25zone/S04_MyWork03.cpp:7919-7995 (the box's
///         one and only implementation). Pity ceiling 200 (Server/Header/Protocol/STRUCT.h:568-571
///         <c>gBox8115</c>; Server/ts25zone/S04_MyWork03.cpp:7923-7929).
///     </para>
///     <para>
///         <b>Unresolved, not invented (open question carried forward from the contract):</b> the item's own
///         in-code comment ("15% Mount Box") is identical wording to the separate, dead-code item-635 block's
///         own comment (<see cref="MountBox635RewardTable" />, whose own remarks flag the same "15%" figure as
///         unexplained by any live roll for THAT item either). Whether 8115's label is an intentional reuse of a
///         display name or a copy-pasted comment cannot be determined from the code alone -- treated here as an
///         in-code label only, not an authoritative name source. Do not infer a weighted table from either
///         item's name.
///     </para>
///     <para>
///         <b>Pity-reward coin flip: a 50/50 uniform draw between ids 1307 and 1315</b> -- the same two ids as
///         <see cref="MountBox635RewardTable.Tiger3" />/<see cref="MountBox635RewardTable.Bear3" />
///         (coincidental id reuse across two independent boxes, not a shared table). No bulk-path counterpart
///         exists for 8115 to diverge from generator-wise (unlike box 8111's bulk-vs-single
///         <c>rand_mir()</c>/<c>rand()</c> split), so this is simply "this function mixes generators," not a
///         fork-divergence, per the contract's own wording.
///     </para>
///     <para>
///         <b>Regular roll (0-149): six banded pools, no separate rare-tier pre-check</b> -- same
///         "pure <see cref="LootBoxRewardResolver.RollPools" /> from the start" shape as its 8111/8114 siblings.
///         <list type="bullet">
///             <item>Pool 1, ceiling 49 (width 50, 33.33%): three items 1307/1315/1319, ~11.11% each.</item>
///             <item>Pool 2, ceiling 69 (width 20, 13.33%): five items 1308/1309/1322/1325/1328, ~2.67% each.</item>
///             <item>
///                 Pool 3, ceiling 79 (width 10, 6.67%): eight items 1301/1302/1303/1313/1317/1320/1323/1326,
///                 ~0.83% each.
///             </item>
///             <item>Pool 4, ceiling 99 (width 20, 13.33%): three items 611/612/652, ~4.44% each.</item>
///             <item>Pool 5, ceiling 129 (width 30, 20.0%): six items 506/507/508/509/578/579, ~3.33% each.</item>
///             <item>
///                 Pool 6, ceiling 149 (width 20, 13.33%): nine items
///                 1166/1118/1103/1222/1145/1237/8101/8102/8106, ~1.48% each.
///             </item>
///         </list>
///     </para>
///     <para>
///         Ordering fact (same as boxes 8111/8114/2249, not a defect): the pity increment/reset happens BEFORE
///         the placement attempt that can still fail. Persistence:
///         <see cref="World.PlayerRuntimeState.MountVariantBoxPity" /> remains session-scoped only, same gap
///         flagged on the sibling pity fields.
///     </para>
/// </remarks>
public static class MountVariantBox8115RewardTable
{
    /// <summary>The box's world.Items id (the item sitting in the opened inventory slot).</summary>
    public const int BoxId = 8115;

    /// <summary>
    ///     Pity ceiling: the open that increments the counter to this value (or beyond) forces a coin-flip draw
    ///     from <see cref="PityRewardItemIds" /> and resets the counter to zero.
    /// </summary>
    public const int PityCeiling = 200;

    /// <summary>The pity-reward coin-flip pool (50/50): ids 1307 and 1315.</summary>
    public static readonly ImmutableArray<int> PityRewardItemIds = [1307, 1315];

    /// <summary>The six regular-roll banded pools over the independent inclusive 0-149 draw.</summary>
    public static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> Pools =
    [
        new(49, [1307, 1315, 1319]),
        new(69, [1308, 1309, 1322, 1325, 1328]),
        new(79, [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326]),
        new(99, [611, 612, 652]),
        new(129, [506, 507, 508, 509, 578, 579]),
        new(149, [1166, 1118, 1103, 1222, 1145, 1237, 8101, 8102, 8106])
    ];

    /// <summary>
    ///     The ready-to-register POST-PITY <see cref="BoxRewardSpec" /> for box 8115 -- see this type's own
    ///     remarks and <see cref="Roll" />. Registering the spec alone (without the
    ///     <c>LootBoxUseItemHandler</c> pity-threading edit) would silently skip the pity gate entirely.
    /// </summary>
    public static readonly BoxRewardSpec Spec =
        BoxRewardSpec.RareBandThenPools(BoxId, ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty, Pools);

    /// <summary>
    ///     The full box-open roll: the pity counter is checked (and incremented) FIRST, unconditionally --
    ///     reaching <see cref="PityCeiling" /> forces a coin-flip draw from <see cref="PityRewardItemIds" />
    ///     (consuming exactly one random draw) and resets the counter to zero. Only when pity has NOT just
    ///     triggered does the regular roll (<see cref="Pools" />) run.
    /// </summary>
    public static MountVariantBoxRollResult Roll(int currentPityCounter, Random random)
    {
        var pity = LootBoxRewardResolver.PityStep(currentPityCounter, PityCeiling);
        if (pity.Triggered)
        {
            var guaranteed = LootBoxRewardResolver.RollUniform(random, PityRewardItemIds);
            return new MountVariantBoxRollResult(guaranteed, pity.NewCounter, true);
        }

        var rewardId = LootBoxRewardResolver.RollPools(random, Pools);
        return new MountVariantBoxRollResult(rewardId, pity.NewCounter, false);
    }

    /// <summary>
    ///     One <see cref="Roll" /> outcome: the resolved reward id, the pity counter value to persist, and
    ///     whether this open was the guaranteed pity hit.
    /// </summary>
    public readonly record struct MountVariantBoxRollResult(int RewardItemId, int NewPityCounter,
        bool WasPityTriggered);
}
