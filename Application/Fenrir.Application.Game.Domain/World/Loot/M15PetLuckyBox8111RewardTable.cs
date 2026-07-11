using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Item 8111 "M15 Pet Lucky Box" reward-pool + pity data (workstream C10-remaining-box-pools). Extends
///     <see cref="LootBoxCatalog" /> with a per-avatar pity counter (ceiling 200, already carried by
///     <see cref="World.PlayerRuntimeState.M15PetLuckyBoxPity" /> since the earlier C10 pass that reserved all
///     four <c>gBox*</c> fields -- see that field's own remarks). Same overall shape as
///     <see cref="CloakBoxRewardTable" />: this file supplies only the table plus the ready-built
///     <see cref="Spec" /> (post-pity roll only) and pity-aware <see cref="Roll" /> composition -- it does not
///     duplicate the roll/placement mechanism, and <c>LootBoxCatalog.cs</c>/<c>LootBoxUseItemHandler.cs</c>
///     wiring is the serial-integration-pass edit described in this workstream's wiring notes.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ (C10-remaining-box-pools contract) : Server/ts25zone/S04_MyWork03.cpp:1612-1672 (bulk-path
///         copy: pity increment/reset/coin-flip, regular roll table) and :7726-7789 (single-path copy, same
///         table, plain <c>rand()</c> pity coin flip vs. the bulk copy's <c>rand_mir()</c> -- a legitimate,
///         independently boot-seeded generator divergence between the two copies, not a security concern by
///         itself, see <see cref="LootBoxRewardResolver" />'s own remarks on this class of divergence). Pity
///         ceiling 200 (Server/Header/Protocol/STRUCT.h:568-571 <c>gBox8111</c>; Server/ts25zone/S04_MyWork03.cpp:1615-1621,7729-7735).
///     </para>
///     <para>
///         <b>Pity-reward coin flip (unlike box 2249's deterministic guarantee): a 50/50 uniform draw between
///         ids 1016 and 1012.</b> One random draw IS consumed on the pity branch here (unlike
///         <see cref="CloakBoxRewardTable" />'s zero-draw guaranteed reward) -- the contract's own wording
///         ("the pity-reward coin flip differs between copies the same way item 720's initial roll does") is
///         explicit that a real coin flip runs, not a fixed id.
///     </para>
///     <para>
///         <b>Regular roll (roll of 0-149, identical in both copies): five banded pools, no separate rare-tier
///         pre-check.</b> Unlike 601/602/2249 (which all roll a rare band against a shared 0..9999 draw before
///         falling through to pools), 8111's ordinary table is a pure <see cref="LootBoxRewardResolver.RollPools" />
///         shape from the start -- the first band (id 1012 alone, 34.0%) is just this table's first pool, not a
///         separately-rated rare tier. <see cref="Spec" /> therefore uses
///         <see cref="BoxRewardKind.RareBandThenPools" /> with an EMPTY <see cref="BoxRewardSpec.RareBands" />
///         array rather than inventing a spurious rare band -- the same "no rare tier" shape
///         <see cref="StellarBox8112RewardTable" />'s own remarks describe for a structurally different reason
///         (there, a fully-covered <see cref="BoxRewardKind.Weighted" /> table; here, an empty-band
///         <see cref="BoxRewardKind.RareBandThenPools" /> table -- both avoid fabricating a rate that is not in
///         the source). Note this is a shape choice for <see cref="Spec" /> only (never actually invoked in
///         production -- the pity override below always intercepts first, exactly like box 2249's own Spec):
///         <see cref="LootBoxRewardResolver.RollRareBandThenPools" /> itself still spends one
///         <c>random.Next(0, RateScale)</c> draw even against an empty band list before falling through to
///         <see cref="LootBoxRewardResolver.RollPools" />, so it is NOT draw-for-draw identical to
///         <see cref="Roll" /> below, which calls <see cref="LootBoxRewardResolver.RollPools" /> directly with
///         no such extra draw -- <see cref="Roll" /> is the actual production roll; <see cref="Spec" />'s own
///         <see cref="BoxRewardSpec.RollRewardId(Random)" /> exists only to satisfy the catalog union's shape.
///         <list type="bullet">
///             <item>Pool 1, ceiling 50 (width 51, 34.0%): single fixed item 1012.</item>
///             <item>Pool 2, ceiling 80 (width 30, 20.0%): three items 1013/1014/1015, 6.67% each.</item>
///             <item>Pool 3, ceiling 100 (width 20, 13.33%): three items 1190/1491/1492, 4.44% each.</item>
///             <item>Pool 4, ceiling 130 (width 30, 20.0%): six items 506/507/508/509/578/579, 3.33% each.</item>
///             <item>
///                 Pool 5, ceiling 149 (width 19, 12.67%): nine items
///                 1166/1118/1103/1222/1145/1237/8101/8102/8106, ~1.41% each.
///             </item>
///         </list>
///     </para>
///     <para>
///         <b>Ordering fact, not a defect to fix (contract's own flag):</b> the pity increment -- and, when it
///         crosses the ceiling, the accompanying reset to zero -- happens BEFORE the eventual placement attempt
///         that can still fail (e.g. a full inventory); a subsequent placement failure does not roll either
///         back. <see cref="Roll" /> itself is pure and does not enforce this ordering -- the caller (the
///         reward-id-override closure in <c>LootBoxUseItemHandler</c>, mirroring box 2249's own wiring) is what
///         must write the returned counter BEFORE the placement step runs, exactly as box 2249 already does.
///     </para>
///     <para>
///         Persistence: <see cref="World.PlayerRuntimeState.M15PetLuckyBoxPity" /> remains session-scoped only
///         (no game.Characters column, hydration, or write-behind flush yet), same gap already flagged on that
///         field and on box 2249's <c>CloakLuckyBoxPity</c> -- unchanged by this workstream.
///     </para>
/// </remarks>
public static class M15PetLuckyBox8111RewardTable
{
    /// <summary>The box's world.Items id (the item sitting in the opened inventory slot).</summary>
    public const int BoxId = 8111;

    /// <summary>
    ///     Pity ceiling: the open that increments the counter to this value (or beyond) forces a coin-flip
    ///     draw from <see cref="PityRewardItemIds" /> and resets the counter to zero.
    /// </summary>
    public const int PityCeiling = 200;

    /// <summary>The pity-reward coin-flip pool (50/50): ids 1012 and 1016.</summary>
    public static readonly ImmutableArray<int> PityRewardItemIds = [1012, 1016];

    /// <summary>The five regular-roll banded pools over the independent inclusive 0-149 draw.</summary>
    public static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> Pools =
    [
        new(50, [1012]),
        new(80, [1013, 1014, 1015]),
        new(100, [1190, 1491, 1492]),
        new(130, [506, 507, 508, 509, 578, 579]),
        new(149, [1166, 1118, 1103, 1222, 1145, 1237, 8101, 8102, 8106])
    ];

    /// <summary>
    ///     The ready-to-register POST-PITY <see cref="BoxRewardSpec" /> for box 8111 (regular roll only -- the
    ///     pity gate itself is NOT part of this pure, box-identity-only spec; see <see cref="Roll" /> and this
    ///     workstream's wiring notes for how the pity gate composes against it). Add this to
    ///     <see cref="LootBoxCatalog" />'s <c>specs</c> list ONLY together with the
    ///     <c>LootBoxUseItemHandler</c> pity-threading edit -- registering the spec alone would silently skip
    ///     the pity gate entirely, exactly the same caveat <see cref="CloakBoxRewardTable.Spec" /> documents.
    /// </summary>
    public static readonly BoxRewardSpec Spec =
        BoxRewardSpec.RareBandThenPools(BoxId, ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty, Pools);

    /// <summary>
    ///     The full box-open roll: the pity counter is checked (and incremented) FIRST, unconditionally, before
    ///     any random draw -- reaching <see cref="PityCeiling" /> forces a coin-flip draw from
    ///     <see cref="PityRewardItemIds" /> (consuming exactly one random draw) and resets the counter to zero.
    ///     Only when pity has NOT just triggered does the regular roll (<see cref="Pools" />, via
    ///     <see cref="LootBoxRewardResolver.RollPools" />) run.
    /// </summary>
    public static M15PetLuckyBoxRollResult Roll(int currentPityCounter, Random random)
    {
        var pity = LootBoxRewardResolver.PityStep(currentPityCounter, PityCeiling);
        if (pity.Triggered)
        {
            var guaranteed = LootBoxRewardResolver.RollUniform(random, PityRewardItemIds);
            return new M15PetLuckyBoxRollResult(guaranteed, pity.NewCounter, true);
        }

        var rewardId = LootBoxRewardResolver.RollPools(random, Pools);
        return new M15PetLuckyBoxRollResult(rewardId, pity.NewCounter, false);
    }

    /// <summary>
    ///     One <see cref="Roll" /> outcome: the resolved reward id, the pity counter value to persist, and
    ///     whether this open was the guaranteed pity hit (for logging/audit -- placement itself is identical
    ///     either way).
    /// </summary>
    public readonly record struct M15PetLuckyBoxRollResult(int RewardItemId, int NewPityCounter,
        bool WasPityTriggered);
}
