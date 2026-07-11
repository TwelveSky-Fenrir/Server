using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Item 2249 "Cloak Box" reward-pool + pity data (workstream C10-cloakbox2249). Extends
///     <see cref="LootBoxCatalog" /> with the one box its own remarks flagged as unregistered pending this
///     box's pity/fallback data ("2249... symbolic ANIMAL_NUM_*/IPET/helper draws... NOT registered"). Same
///     data-only posture as <see cref="PetBoxRewardTable" />/<see cref="StellarBox8112RewardTable" />: this file
///     supplies only the table plus the ready-built <see cref="Spec" /> and pity-aware <see cref="Roll" />
///     composition -- it does not duplicate the roll/placement mechanism, and <c>LootBoxCatalog.cs</c> itself is
///     not edited here per the concurrent-wave "new files only" rule (see this slice's wiring manifest).
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ (per the C10-cloakbox2249 behavior contract, itself citing):
///         Server/ts25zone/S04_MyWork03.cpp:1097-1171
///         (<c>RandomCloak</c>, the complete shared reward routine) ; :1105-1114 (the pity counter <c>gBox2249</c>
///         -- increments unconditionally before any roll, forces the guaranteed reward and resets to zero at
///         ceiling 100) ; :1121-1161 (the post-pity roll: a 0-9999 draw for the 0.6%/60-in-10000 special-reward
///         band, and -- only on a miss -- an independent 0-199 draw selecting one of two six-item pools followed
///         by a 0-5 uniform draw within that pool). Item names: 1401 "Ultimate Cloak"
///         (Server/BuildEU33/ITEM_DUMP_CLEAN.csv:1022), 1403 "Warlord Cape" (:1024).
///     </para>
///     <para>
///         <b>Why <see cref="LootBoxRewardResolver.RollCloak" /> is deliberately NOT used here.</b> That
///         existing primitive was pre-built generically (before this box's concrete table was recoverable) with
///         a flat-<see cref="LootBoxRewardResolver.WeightedReward" /> fallback -- a single 0..totalWeight draw.
///         The C10-cloakbox2249 contract instead describes an explicit two-stage fallback (an independent
///         0-199 pool-select draw, THEN a separate 0-5 within-pool draw) -- exactly the shape
///         <see cref="LootBoxRewardResolver.RollRareBandThenPools" /> (via <see cref="LootBoxRewardResolver.RollPools" />)
///         already implements, and the same shape the 601 Mount Box / 602 Pet Box entries already use. Using
///         <c>RollCloak</c> here would consume the wrong number of random draws (breaking the contract's own
///         "zero draws on pity trigger, one draw on a special hit, three draws on a pool fallback" accounting)
///         and would require flattening two unevenly-divisible pool widths (101/99 across 6 items each) into
///         per-item weights that cannot exactly reproduce the two-stage draw. <see cref="Roll" /> therefore
///         composes <see cref="LootBoxRewardResolver.PityStep" /> (the pure pity-counter half, already shared
///         with the still-unregistered 8111/8114/8115 boxes) with
///         <see cref="LootBoxRewardResolver.RollRareBandThenPools" /> directly -- no new roll primitive, no
///         duplicated mechanism, only this box's own concrete ids/thresholds.
///     </para>
///     <para>
///         <b>Special tier (60-in-10000, 0.6%): item 1403 "Warlord Cape".</b> One rare band, first (and only)
///         hit wins; a miss falls through to <see cref="Pools" />.
///     </para>
///     <para>
///         <b>Fallback pools (99.4% combined, two bands over an inclusive 0-199 draw).</b>
///         <list type="bullet">
///             <item>
///                 Pool 1, ceiling 100 (width 101, 50.5% absolute): six consumable-potion-family items
///                 506/507/508/509/578/579, ~8.42% each.
///             </item>
///             <item>
///                 Pool 2, ceiling 199 (width 99, 49.5% absolute): six charm/scroll-family items
///                 1166/1118/1103/1222/1145/1237, 8.25% each.
///             </item>
///         </list>
///         Both pools are the exact same twelve ids (same two families) the 601 Mount Box's own last two bands
///         and 602 Pet Box's own last two bands reuse -- corroborating, not coincidental, since all three boxes
///         share the same legacy "ordinary consumable + ordinary charm" fallback tier convention.
///     </para>
///     <para>
///         <b>Pity (ceiling 100): item 1401 "Ultimate Cloak".</b> The 100th open (and every 100th open after a
///         reset) is forced to 1401 with zero random draws consumed, and the counter resets to zero --
///         <see cref="PlayerRuntimeState.CloakLuckyBoxPity" /> is the mutable per-avatar counter this composes
///         against (see this slice's wiring manifest for how the pity state reaches this pure function).
///     </para>
///     <para>
///         <b>
///             Deliberate legacy-parity choice: pity commit is NOT rolled back on a subsequent placement
///             failure.
///         </b>
///         The C10-cloakbox2249 contract explicitly flags this as "a legacy-parity decision point,
///         not a defect to silently fix" and asks Fenrir to choose explicitly. This slice chooses to reproduce
///         legacy behavior byte-for-byte: the wiring manifest's reward-id-override closure (which calls
///         <see cref="Roll" /> and durably writes the returned counter onto <c>PlayerRuntimeState</c>) executes
///         BEFORE <c>LootBoxOpenResolver.OpenSingle</c>'s own item-lookup/placement steps run, so a pity-
///         triggering box that then fails to place (inventory-full, only reachable when that box is not its
///         own stack's last unit) still leaves the counter reset to zero and the guaranteed 1401 ungranted --
///         matching the C++ exactly. No different behavior is invented.
///     </para>
///     <para>
///         <b>Not modeled here (open questions, carried forward, not invented):</b> (1) the contract's own
///         "hardcoded Rare-tier serial number for 1401/1403" open question -- <c>BoxRewardPlacementResolver</c>
///         has no per-reward serial-tier override hook at all today, so this is a pre-existing placement-layer
///         gap, not something this data file can close. (2) The contract's "historical removed special-reward
///         tier" open question (a dead comment referencing items 1404/1407/92289 once alongside 1403) --
///         explicitly flagged by the contract as unrecoverable; not reconstructed here. (3) Persistence:
///         <see cref="PlayerRuntimeState.CloakLuckyBoxPity" /> remains session-scoped only (no game.Characters
///         column, hydration, or write-behind flush yet) per that field's own remarks -- unchanged by this
///         slice.
///     </para>
/// </remarks>
public static class CloakBoxRewardTable
{
    /// <summary>The box's world.Items id (the item sitting in the opened inventory slot).</summary>
    public const int BoxId = 2249;

    /// <summary>
    ///     Pity ceiling: the open that increments the counter to this value (or beyond) forces
    ///     <see cref="GuaranteedRewardItemId" /> and resets the counter to zero.
    /// </summary>
    public const int PityCeiling = 100;

    /// <summary>Item 1401 "Ultimate Cloak" -- the guaranteed pity reward.</summary>
    public const int GuaranteedRewardItemId = 1401;

    /// <summary>The single special-tier rare band (0.6% = 60-in-10000): item 1403 "Warlord Cape".</summary>
    public static readonly ImmutableArray<LootBoxRewardResolver.RewardBand> RareBands =
    [
        new(60, 1403)
    ];

    /// <summary>Pool 1 (potion family), selected by the pool-select draw landing in 0..100 inclusive.</summary>
    public static readonly ImmutableArray<int> PotionPoolIds = [506, 507, 508, 509, 578, 579];

    /// <summary>Pool 2 (charm/scroll family), selected by the pool-select draw landing in 101..199 inclusive.</summary>
    public static readonly ImmutableArray<int> ScrollCharmPoolIds = [1166, 1118, 1103, 1222, 1145, 1237];

    /// <summary>
    ///     The two fallback-tier banded pools over the independent inclusive 0-199 draw -- ceilings are each
    ///     pool's cumulative upper bound, matching <see cref="LootBoxRewardResolver.RollPools" />'s "first pool
    ///     whose ceiling is at or above the draw wins" semantics.
    /// </summary>
    public static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> Pools =
    [
        new(100, PotionPoolIds),
        new(199, ScrollCharmPoolIds)
    ];

    /// <summary>
    ///     The ready-to-register POST-PITY <see cref="BoxRewardSpec" /> for box 2249 (special roll + fallback
    ///     pools only -- the pity gate itself is NOT part of this pure, box-identity-only spec, since
    ///     <see cref="BoxRewardSpec" /> is a process-wide singleton that cannot hold any per-avatar mutable
    ///     state; see <see cref="Roll" /> and this slice's wiring manifest for how the pity gate composes
    ///     against it). Add this to <see cref="LootBoxCatalog" />'s <c>specs</c> list ONLY together with the
    ///     <c>LootBoxOpenResolver</c>/<c>LootBoxUseItemHandler</c> pity-threading edits in the same wiring
    ///     manifest -- registering the spec alone would silently skip the pity gate entirely.
    /// </summary>
    public static readonly BoxRewardSpec Spec = BoxRewardSpec.RareBandThenPools(BoxId, RareBands, Pools);

    /// <summary>
    ///     The full box-open roll: the pity counter is checked (and incremented) FIRST, unconditionally, before
    ///     any random draw -- reaching <see cref="PityCeiling" /> forces <see cref="GuaranteedRewardItemId" />
    ///     and resets the counter to zero, consuming zero draws from <paramref name="random" />. Only when pity
    ///     has NOT just triggered does the post-pity roll (special band, else fallback pools) run via
    ///     <see cref="Spec" />'s own <see cref="BoxRewardSpec.RollRewardId" /> shape (reproduced here directly
    ///     against <see cref="RareBands" />/<see cref="Pools" /> rather than through <see cref="Spec" /> itself,
    ///     since the pity branch must short-circuit before any spec-level roll).
    /// </summary>
    public static CloakBoxRollResult Roll(int currentPityCounter, Random random)
    {
        var pity = LootBoxRewardResolver.PityStep(currentPityCounter, PityCeiling);
        if (pity.Triggered)
            return new CloakBoxRollResult(GuaranteedRewardItemId, pity.NewCounter, true);

        var rewardId = LootBoxRewardResolver.RollRareBandThenPools(random, RareBands, Pools);
        return new CloakBoxRollResult(rewardId, pity.NewCounter, false);
    }

    /// <summary>
    ///     One <see cref="Roll" /> outcome: the resolved reward id, the pity counter value to persist, and
    ///     whether this open was the guaranteed pity hit (for logging/audit -- placement itself is identical
    ///     either way).
    /// </summary>
    public readonly record struct CloakBoxRollResult(int RewardItemId, int NewPityCounter, bool WasPityTriggered);
}
