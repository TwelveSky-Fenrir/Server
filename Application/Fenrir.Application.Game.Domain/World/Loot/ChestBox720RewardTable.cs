using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Reward-table DATA and roll primitive for item 720 ("Chest / Reward Box" per in-code comment only --
///     display name is an open question, not confirmed against any authoritative item-name source) -- workstream
///     C10-remaining-box-pools. <b>Fully recovered</b> as of a fresh, targeted legacy re-verification pass: both
///     the tribe-keyed &lt;15% branch and the previously un-modeled 85% branch (whose <c>GetRandomAnimal5()</c>
///     8-id pool could not be located in an earlier pass) are now modeled below.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ (C10-remaining-box-pools contract) : Server/ts25zone/S04_MyWork03.cpp:1372-1439 (bulk-path
///         copy) and :6210-6273 (single-path copy) -- both re-read this session per the contract; confirmed the
///         <c>rand_mir()</c>-vs-<c>rand()</c> generator split between the two copies (a legitimate, independently
///         boot-seeded divergence, not a security concern by itself) and the single-path copy's MISSING
///         empty-pool guard for an unrecognized previous-tribe value (see "Hardening choice" below). Roll of
///         0-99.
///     </para>
///     <para>
///         <b>Branch layout:</b>
///         <list type="bullet">
///             <item>
///                 Below 15 (15%): a 7-item pool keyed on previous-tribe -- modeled by <see cref="Roll" /> below.
///                 Base ids 15157/15267/15135/15179/15223/15245/15289, one drawn uniformly, THEN offset by the
///                 tribe: tribe 0 -&gt; +0, tribe 1 -&gt; +20000, tribe 2 -&gt; +40000.
///             </item>
///             <item>
///                 15 and above (85%): an 8-slot pool independent of tribe, each slot equally likely (1-in-8) --
///                 see the next paragraph for the full slot list.
///             </item>
///         </list>
///     </para>
///     <para>
///         <b>The 85% branch's 8 equally-likely slots</b> (Server/ts25zone/S04_MyWork03.cpp:1372-1439 and
///         :6210-6273, both case bodies build this same 8-slot pool):
///         <list type="number">
///             <item>
///                 <c>GetRandomAnimal5()</c> (Server/ts25zone/S07_MyGame03.cpp:7342-7355): a uniform draw over 8
///                 tier-1 animal ids -- 1301 (tiger), 1302 (pig), 1303 (deer), 1313 (bear), 1317 (cat), 1320
///                 (bull), 1323 (wolf), 1326 (lion), per Server/Header/Protocol/DEFINE.h:157,160,163,166,169,172,
///                 175,178 (<c>ANIMAL_NUM_TIGER1</c>/<c>_PIG1</c>/<c>_DEER1</c>/<c>_BEAR1</c>/<c>_CAT1</c>/
///                 <c>_BULL1</c>/<c>_WOLF1</c>/<c>_LION1</c>). This is the SAME 8-id pool box 601's own second
///                 pool already uses (<see cref="LootBoxCatalog" />'s own <c>601</c> spec) -- cross-confirms the
///                 ids.
///             </item>
///             <item>Fixed id 1449.</item>
///             <item>Fixed id 1072.</item>
///             <item>
///                 <c>GetRandomElixirPlus()</c> (Server/ts25zone/S07_MyGame03.cpp:7319-7322): a uniform draw over
///                 6 ids -- 801, 802, 803, 804, 805, 806.
///             </item>
///             <item>Fixed id 1437.</item>
///             <item>Fixed id 1178.</item>
///             <item>Fixed id 698.</item>
///             <item>Fixed id 1166.</item>
///         </list>
///         The legacy source always builds/evaluates BOTH composite slots (the animal draw and the elixir-plus
///         draw) regardless of which of the 8 slots the final draw actually selects -- a build-then-select
///         ordering that does not change the OUTPUT distribution (still exactly 1-in-8 per slot, 1-in-64 per
///         animal id, 1-in-48 per elixir-plus id). <see cref="Roll" /> below deliberately does NOT reproduce that
///         RNG-consumption-order quirk (it only draws the composite pool actually selected) -- this codebase does
///         not reproduce RNG-consumption-order artifacts anywhere else in this reward-table family either (see
///         e.g. <see cref="HeavenlyJadeChest1236RewardTable" />'s own single-draw-per-branch shape), and doing so
///         here would have zero observable effect on the granted reward.
///     </para>
///     <para>
///         <b>Hardening choice for the 15% branch (this project's "harden, never reproduce" posture, not a
///         legacy citation):</b> the bulk-path copy explicitly checks for an empty resulting pool and fails the
///         request if the previous-tribe value matched no branch; the single-path copy has NO such guard and
///         would attempt to draw from a zero-length pool (a division/modulo-by-zero condition in the original
///         C++). <see cref="Roll" /> below always takes the bulk-path's safer, fail-closed shape for an
///         unrecognized tribe, regardless of which physical open path a given request takes in Fenrir -- the
///         same choice already made for <see cref="HeavenlyJadeChest1236RewardTable" />'s identical divergence
///         shape. The 85% branch has no tribe dependency at all, so this hardening choice only affects the 15%
///         branch.
///     </para>
/// </remarks>
public static class ChestBox720RewardTable
{
    /// <summary>world.Items id for this box.</summary>
    public const int BoxId = 720;

    /// <summary>Roll threshold (exclusive upper bound): the tribe-keyed 15% branch.</summary>
    public const int TribePoolThresholdExclusive = 15;

    /// <summary>The 7 base ids of the tribe-keyed pool, before the tribe offset is added.</summary>
    public static readonly ImmutableArray<int> TribePoolBaseIds =
        [15157, 15267, 15135, 15179, 15223, 15245, 15289];

    /// <summary>The additive offset applied to a drawn base id, keyed by previous-tribe code.</summary>
    public static readonly FrozenDictionary<byte, int> TribeOffsetByPreviousTribe =
        new Dictionary<byte, int> { [0] = 0, [1] = 20000, [2] = 40000 }.ToFrozenDictionary();

    // ---- The 85% branch's 8 equally-likely (1-in-8) slots, independent of tribe. Slot numbers below match
    // this type's own remarks (1-indexed, matching the contract's own slot list).

    /// <summary>
    ///     85% branch, slot 1 of 8: <c>GetRandomAnimal5()</c>'s 8-id uniform sub-pool (tiger/pig/deer/bear/cat/
    ///     bull/wolf/lion tier-1 animal ids). Same pool box 601's own second pool already registers.
    /// </summary>
    public static readonly ImmutableArray<int> AnimalPoolIds = [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326];

    /// <summary>85% branch, slot 2 of 8: fixed id (name not recovered).</summary>
    public const int FixedRewardIdSlot2 = 1449;

    /// <summary>85% branch, slot 3 of 8: fixed id (name not recovered).</summary>
    public const int FixedRewardIdSlot3 = 1072;

    /// <summary>85% branch, slot 4 of 8: <c>GetRandomElixirPlus()</c>'s 6-id uniform sub-pool.</summary>
    public static readonly ImmutableArray<int> ElixirPlusPoolIds = [801, 802, 803, 804, 805, 806];

    /// <summary>85% branch, slot 5 of 8: fixed id (name not recovered).</summary>
    public const int FixedRewardIdSlot5 = 1437;

    /// <summary>85% branch, slot 6 of 8: fixed id (name not recovered).</summary>
    public const int FixedRewardIdSlot6 = 1178;

    /// <summary>85% branch, slot 7 of 8: fixed id (name not recovered).</summary>
    public const int FixedRewardIdSlot7 = 698;

    /// <summary>85% branch, slot 8 of 8: fixed id (name not recovered).</summary>
    public const int FixedRewardIdSlot8 = 1166;

    /// <summary>
    ///     Rolls this box's outcome for a given opener. <see cref="RollResult.Success" /> is false only for an
    ///     unrecognized <paramref name="previousTribe" /> landing in the tribe-keyed &lt;15% branch (hardening
    ///     choice, see this type's own remarks) -- the 85% branch always succeeds, since it has no tribe
    ///     dependency at all.
    /// </summary>
    public static RollResult Roll(byte previousTribe, Random random)
    {
        var outer = random.Next(0, 100);

        if (outer < TribePoolThresholdExclusive)
        {
            if (!TribeOffsetByPreviousTribe.TryGetValue(previousTribe, out var offset))
                return RollResult.Failure; // unrecognized tribe -- hardening choice, see this type's own remarks.

            var baseId = TribePoolBaseIds[random.Next(0, TribePoolBaseIds.Length)];
            return new RollResult(true, baseId + offset);
        }

        // 85% branch: 8 equally-likely outer slots, independent of tribe -- see this type's own remarks.
        var slot = random.Next(0, 8);
        return slot switch
        {
            0 => new RollResult(true, LootBoxRewardResolver.RollUniform(random, AnimalPoolIds)),
            1 => new RollResult(true, FixedRewardIdSlot2),
            2 => new RollResult(true, FixedRewardIdSlot3),
            3 => new RollResult(true, LootBoxRewardResolver.RollUniform(random, ElixirPlusPoolIds)),
            4 => new RollResult(true, FixedRewardIdSlot5),
            5 => new RollResult(true, FixedRewardIdSlot6),
            6 => new RollResult(true, FixedRewardIdSlot7),
            _ => new RollResult(true, FixedRewardIdSlot8)
        };
    }

    /// <summary>Outcome of <see cref="Roll" />. RewardItemId is only meaningful when Success is true.</summary>
    public readonly record struct RollResult(bool Success, int RewardItemId)
    {
        public static RollResult Failure { get; } = new(false, 0);
    }
}
