using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Reward-table DATA and roll primitive for item 8005 ("Wing Lucky Box") -- workstream
///     C10-remaining-box-pools, mirroring the legacy shared helper <c>RandomWingBox</c>. Like
///     <see cref="HeavenlyJadeChest1236RewardTable" />, three of its five branches are keyed by the opening
///     character's own stored previous-tribe code, which <see cref="BoxRewardSpec.RollRewardId(Random)" /> has
///     no parameter to receive. This file supplies pure DATA plus a self-contained, fully-tested roll primitive
///     (<see cref="Roll" />) rather than a <see cref="BoxRewardSpec" />; wiring reuses the same reward-id-
///     override seam as 76543/1378/1379/1236, deliberately NOT made here.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ (C10-remaining-box-pools contract) : Server/ts25zone/S04_MyWork03.cpp:1172-1281
///         (<c>RandomWingBox</c>, the ONE shared implementation invoked identically from both the bulk call site,
///         :1775-1777, and the single-path call site, :8080-8082 -- so there is no bulk-vs-single divergence
///         risk for this box at all, unlike 720/1236/8108/8111).
///     </para>
///     <para>
///         <b>First roll (0-9999): two special tribe-keyed tiers, both explicitly fail-closed on an unrecognized
///         tribe.</b> This is the ONLY one of this workstream's eight boxes whose EVERY tribe-keyed branch
///         already fails the request explicitly in the ONE shared legacy implementation, rather than leaving the
///         reward field unassigned as a passthrough risk -- so <see cref="Roll" /> below reproduces this
///         directly, no hardening choice required (contrast <see cref="HeavenlyJadeChest1236RewardTable" />'s
///         own remarks, where the single-path copy lacks this guard and Fenrir must choose the safer of two
///         diverging legacy shapes).
///         <list type="bullet">
///             <item>
///                 0-79 (0.8%): a previous-tribe-keyed "Blue Dragon Wings" id (0-&gt;213, 1-&gt;214, 2-&gt;215).
///             </item>
///             <item>
///                 80-129 (0.5%): a previous-tribe-keyed "Archangel Wings" id (0-&gt;216, 1-&gt;217, 2-&gt;218).
///             </item>
///             <item>130-9999 (99.3%): falls through to the second roll below.</item>
///         </list>
///     </para>
///     <para>
///         <b>Second roll (0-199, independent draw, only reached on a miss above):</b>
///         <list type="bullet">
///             <item>0-5 (6-in-200, 3.0% of the 99.3%): fixed id 2477.</item>
///             <item>
///                 6-60 (55-in-200, 27.5% of the 99.3%): a previous-tribe-keyed single id (0-&gt;201, 1-&gt;202,
///                 2-&gt;203), again explicitly failing on an unmatched tribe.
///             </item>
///             <item>61-100 (40-in-200, 20.0% of the 99.3%): uniform over 2397/694/693/692/696/698.</item>
///             <item>101-160 (60-in-200, 30.0% of the 99.3%): uniform over 506/507/508/509/578/579.</item>
///             <item>161-199 (39-in-200, 19.5% of the 99.3%): uniform over 1166/1118/1103/1222/1145/1237.</item>
///         </list>
///     </para>
/// </remarks>
public static class WingLuckyBox8005RewardTable
{
    /// <summary>world.Items id for the Wing Lucky Box itself.</summary>
    public const int BoxId = 8005;

    /// <summary>First-roll threshold (exclusive upper bound, 0-79): the Blue Dragon Wings band.</summary>
    public const int BlueDragonWingsThresholdExclusive = 80;

    /// <summary>First-roll threshold (exclusive upper bound, 80-129): the Archangel Wings band.</summary>
    public const int ArchangelWingsThresholdExclusive = 130;

    /// <summary>Previous-tribe-keyed "Blue Dragon Wings" ids.</summary>
    public static readonly FrozenDictionary<byte, int> BlueDragonWingsIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 213, [1] = 214, [2] = 215 }.ToFrozenDictionary();

    /// <summary>Previous-tribe-keyed "Archangel Wings" ids.</summary>
    public static readonly FrozenDictionary<byte, int> ArchangelWingsIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 216, [1] = 217, [2] = 218 }.ToFrozenDictionary();

    /// <summary>Item 2477 -- the second roll's 0-5 fixed reward.</summary>
    public const int FixedRewardId2477 = 2477;

    /// <summary>Second-roll threshold (inclusive ceiling, 0-5): the fixed 2477 band.</summary>
    public const int FixedRewardCeilingInclusive = 5;

    /// <summary>Second-roll threshold (inclusive ceiling, 6-60): the tribe-keyed band.</summary>
    public const int TribeSecondCeilingInclusive = 60;

    /// <summary>Previous-tribe-keyed second-tier id.</summary>
    public static readonly FrozenDictionary<byte, int> TribeSecondIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 201, [1] = 202, [2] = 203 }.ToFrozenDictionary();

    /// <summary>Second-roll threshold (inclusive ceiling, 61-100).</summary>
    public const int MiscPoolCeilingInclusive = 100;

    /// <summary>61-100 pool.</summary>
    public static readonly ImmutableArray<int> MiscPoolIds = [2397, 694, 693, 692, 696, 698];

    /// <summary>Second-roll threshold (inclusive ceiling, 101-160).</summary>
    public const int ElixirPoolCeilingInclusive = 160;

    /// <summary>101-160 pool (consumable-potion family).</summary>
    public static readonly ImmutableArray<int> ElixirPoolIds = [506, 507, 508, 509, 578, 579];

    /// <summary>161-199 pool (charm/scroll family). The second roll's range top (199) is this pool's ceiling.</summary>
    public static readonly ImmutableArray<int> CharmPoolIds = [1166, 1118, 1103, 1222, 1145, 1237];

    /// <summary>
    ///     Rolls this box's full outcome for a given opener. <see cref="RollResult.Success" /> is false for an
    ///     unrecognized <paramref name="previousTribe" /> on any of the three tribe-keyed branches -- matching
    ///     the ONE shared legacy implementation's own explicit fail-outright guard on every such branch (no
    ///     hardening choice needed here, unlike 1236/720/8108 -- see this type's own remarks).
    /// </summary>
    public static RollResult Roll(byte previousTribe, Random random)
    {
        var first = random.Next(0, 10_000);

        if (first < BlueDragonWingsThresholdExclusive)
            return BlueDragonWingsIdByPreviousTribe.TryGetValue(previousTribe, out var blueDragonId)
                ? new RollResult(true, blueDragonId)
                : RollResult.Failure;

        if (first < ArchangelWingsThresholdExclusive)
            return ArchangelWingsIdByPreviousTribe.TryGetValue(previousTribe, out var archangelId)
                ? new RollResult(true, archangelId)
                : RollResult.Failure;

        var second = random.Next(0, 200);

        if (second <= FixedRewardCeilingInclusive)
            return new RollResult(true, FixedRewardId2477);

        if (second <= TribeSecondCeilingInclusive)
            return TribeSecondIdByPreviousTribe.TryGetValue(previousTribe, out var tribeSecondId)
                ? new RollResult(true, tribeSecondId)
                : RollResult.Failure;

        if (second <= MiscPoolCeilingInclusive)
            return new RollResult(true, LootBoxRewardResolver.RollUniform(random, MiscPoolIds));

        if (second <= ElixirPoolCeilingInclusive)
            return new RollResult(true, LootBoxRewardResolver.RollUniform(random, ElixirPoolIds));

        return new RollResult(true, LootBoxRewardResolver.RollUniform(random, CharmPoolIds));
    }

    /// <summary>Outcome of <see cref="Roll" />. RewardItemId is only meaningful when Success is true.</summary>
    public readonly record struct RollResult(bool Success, int RewardItemId)
    {
        public static RollResult Failure { get; } = new(false, 0);
    }
}
