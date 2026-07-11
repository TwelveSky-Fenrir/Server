using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Item 8114 (no in-code display name -- workstream C10-remaining-box-pools) reward-pool + pity data.
///     Single-open only: this box has NO bulk-path counterpart at all in the legacy source (confirmed absent
///     from <see cref="LootBoxCatalog.BulkOpenWhitelist" />), so a client-requested bulk count on this item has
///     zero effect on outcome -- exactly one box opens per request regardless. Uses the already-reserved
///     per-avatar pity counter <see cref="World.PlayerRuntimeState.CloakVariantBoxPity" /> (ceiling 200).
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ (C10-remaining-box-pools contract) : Server/ts25zone/S04_MyWork03.cpp:7863-7917 (the box's
///         one and only implementation -- single-path only, no bulk copy exists to diverge from). Pity ceiling
///         200 (Server/Header/Protocol/STRUCT.h:568-571 <c>gBox8114</c>; Server/ts25zone/S04_MyWork03.cpp:7866-7872).
///     </para>
///     <para>
///         <b>Pity reward is deterministic: item 1403, no coin flip, zero random draws consumed on trigger</b> --
///         unlike <see cref="M15PetLuckyBox8111RewardTable" />'s 1012/1016 coin flip. This is the same "zero
///         draws on the guaranteed hit" shape <see cref="CloakBoxRewardTable" /> (box 2249, guaranteed 1401)
///         already uses.
///     </para>
///     <para>
///         <b>Regular roll (0-499): four banded pools, no separate rare-tier pre-check</b> -- the same "pure
///         <see cref="LootBoxRewardResolver.RollPools" /> from the start" shape
///         <see cref="M15PetLuckyBox8111RewardTable" /> uses, for the same reason (no rate-scaled rare check
///         exists in the source ahead of these bands): <see cref="Spec" /> uses
///         <see cref="BoxRewardKind.RareBandThenPools" /> with an empty <see cref="BoxRewardSpec.RareBands" />
///         array.
///         <list type="bullet">
///             <item>
///                 Pool 1, ceiling 1 (width 2, 0.4%): item 1403 -- the SAME id the pity reward grants, reachable
///                 "naturally" too (not merged with the pity branch: this is a genuinely independent roll
///                 outcome that happens to coincide).
///             </item>
///             <item>Pool 2, ceiling 49 (width 48, 9.6%): two items 92290/1401, 4.8% each.</item>
///             <item>Pool 3, ceiling 249 (width 200, 40.0%): six items 506/507/508/509/578/579, ~6.67% each.</item>
///             <item>
///                 Pool 4, ceiling 499 (width 250, 50.0%): nine items
///                 1166/1118/1103/1222/1145/1237/8101/8102/8106, ~5.56% each.
///             </item>
///         </list>
///     </para>
///     <para>
///         Ordering fact (same as box 8111/2249, not a defect): the pity increment/reset happens BEFORE the
///         placement attempt that can still fail; a subsequent placement failure does not roll either back.
///         Persistence: <see cref="World.PlayerRuntimeState.CloakVariantBoxPity" /> remains session-scoped only,
///         same gap flagged on the sibling pity fields.
///     </para>
/// </remarks>
public static class CloakVariantBox8114RewardTable
{
    /// <summary>The box's world.Items id (the item sitting in the opened inventory slot).</summary>
    public const int BoxId = 8114;

    /// <summary>
    ///     Pity ceiling: the open that increments the counter to this value (or beyond) forces
    ///     <see cref="GuaranteedRewardItemId" /> (zero random draws) and resets the counter to zero.
    /// </summary>
    public const int PityCeiling = 200;

    /// <summary>Item 1403 -- the deterministic pity reward (no coin flip, unlike box 8111/8115).</summary>
    public const int GuaranteedRewardItemId = 1403;

    /// <summary>The four regular-roll banded pools over the independent inclusive 0-499 draw.</summary>
    public static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> Pools =
    [
        new(1, [1403]),
        new(49, [92290, 1401]),
        new(249, [506, 507, 508, 509, 578, 579]),
        new(499, [1166, 1118, 1103, 1222, 1145, 1237, 8101, 8102, 8106])
    ];

    /// <summary>
    ///     The ready-to-register POST-PITY <see cref="BoxRewardSpec" /> for box 8114 -- see this type's own
    ///     remarks and <see cref="Roll" />. Registering the spec alone (without the
    ///     <c>LootBoxUseItemHandler</c> pity-threading edit) would silently skip the pity gate entirely.
    /// </summary>
    public static readonly BoxRewardSpec Spec =
        BoxRewardSpec.RareBandThenPools(BoxId, ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty, Pools);

    /// <summary>
    ///     The full box-open roll: the pity counter is checked (and incremented) FIRST, unconditionally --
    ///     reaching <see cref="PityCeiling" /> forces <see cref="GuaranteedRewardItemId" /> with zero random
    ///     draws consumed and resets the counter to zero. Only when pity has NOT just triggered does the
    ///     regular roll (<see cref="Pools" />) run.
    /// </summary>
    public static CloakVariantBoxRollResult Roll(int currentPityCounter, Random random)
    {
        var pity = LootBoxRewardResolver.PityStep(currentPityCounter, PityCeiling);
        if (pity.Triggered)
            return new CloakVariantBoxRollResult(GuaranteedRewardItemId, pity.NewCounter, true);

        var rewardId = LootBoxRewardResolver.RollPools(random, Pools);
        return new CloakVariantBoxRollResult(rewardId, pity.NewCounter, false);
    }

    /// <summary>
    ///     One <see cref="Roll" /> outcome: the resolved reward id, the pity counter value to persist, and
    ///     whether this open was the guaranteed pity hit.
    /// </summary>
    public readonly record struct CloakVariantBoxRollResult(int RewardItemId, int NewPityCounter,
        bool WasPityTriggered);
}
