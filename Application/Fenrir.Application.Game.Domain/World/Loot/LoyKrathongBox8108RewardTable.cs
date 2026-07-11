using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Reward-table DATA and roll primitive for item 8108 ("Loy Krathong Box") -- workstream
///     C10-remaining-box-pools. <b>Security-hardened by design</b>: the legacy single-open code path for this
///     item contains a confirmed, live arbitrary-item-grant exploit (see this type's own remarks, "SECURITY:
///     legacy single-open passthrough NOT reproduced") -- Fenrir does not implement that path at all. Both
///     single-open and bulk-open of box 8108 in Fenrir resolve through this ONE table
///     (<see cref="Roll" />), which reproduces only the legacy BULK-path's own full, 100%-range-covered,
///     server-authoritative reward table -- the one legitimate, fully-specified table the C10-remaining-box-
///     pools contract found for this item. Like <see cref="HeavenlyJadeChest1236RewardTable" />/
///     <see cref="WingLuckyBox8005RewardTable" />, one band is tribe-keyed, so this does not fit any existing
///     <see cref="BoxRewardKind" /> and is wired via the same reward-id-override seam as 76543/1378/1379/1236/8005,
///     deliberately NOT made here.
/// </summary>
/// <remarks>
///     <para>
///         <b>SECURITY: legacy single-open passthrough NOT reproduced.</b> Per the C10-remaining-box-pools
///         contract's own direct citation (Server/ts25zone/S04_MyWork03.cpp:8128-8145,8141-8142): the legacy
///         single-open case block for item 8108 only covers 68% of its own 0-99 roll range (bands for 0-5, 6-13,
///         14-25, 26-45, 46-67) -- the remaining 68-99 range (32% of all single-open rolls, no special
///         precondition required) has NO code at all, only a commented-out fallback that was never replaced.
///         When that range fires, the reward-id field is left holding whatever value the CLIENT ITSELF supplied
///         in the original request packet (<c>tValue</c>, copied from the raw socket buffer before any box logic
///         runs) -- that value then flows unconditionally into the shared grant routine, which validates it only
///         against "does an item with this id exist," granting ANY existing item id, not just this box's
///         intended pool. This is a live, unconditionally-reachable arbitrary-item-grant vector in the legacy
///         source, not a theoretical one -- exactly the "unbounded currency/item-grant opcode" finding class this
///         project's security-findings catalog (finding #3, <c>ts25-security-findings-catalog</c> skill;
///         mirrored by finding #9's delegated-auth writeup on "never trust a value merely because it transited
///         through code that looks legitimate") flags as a standing "harden, never reproduce" rule. Fenrir's
///         box-open mechanism (<see cref="Inventory.UseItems.Boxes.LootBoxOpenResolver" />) never reads a reward
///         id from the client's request in the first place (the wire packet's <c>tValue</c> field only ever
///         reaches Fenrir as the bulk-open COUNT, via <c>UseItemContext.Value</c> -- see that resolver's own
///         remarks), so this exploit's specific MECHANISM (an unassigned reward-id variable defaulting to a
///         caller-controlled field) has no equivalent code path to reproduce even by accident. This table exists
///         so that box 8108's reward resolution is ALSO fully bounded and server-authoritative at the DATA
///         level, for both single-open and bulk-open, with zero gap in roll coverage -- the bulk-path table
///         below is exhaustive over its full 0-99 range, unlike the legacy single-path table.
///     </para>
///     <para>
///         Réf. C++ (C10-remaining-box-pools contract) : Server/ts25zone/S04_MyWork03.cpp:1523-1610 (bulk-path
///         copy, the full 100%-covered table reproduced here) and :8128-8145 (single-path copy, only 68%
///         covered -- cited above, NOT reproduced). Roll of 0-99.
///     </para>
///     <para>
///         <b>Branch layout (mutually exclusive, exhaustive over 0-99):</b>
///         <list type="bullet">
///             <item>0-5 (6%): fixed id 1407.</item>
///             <item>
///                 6-7 (2%): a re-roll of sevenths, alternating ids 1403/1404 in a 4-versus-3 split (NOT an even
///                 50/50). <b>Open question, flagged, not silently resolved:</b> the contract's own wording
///                 states the two magnitudes (4-in-7 and 3-in-7) but does not separately state WHICH of the two
///                 ids receives the larger share -- unlike the 10-11 band immediately below, where the contract
///                 is explicit ("826 on a third of draws and 619 on two-thirds"). <see cref="Roll" /> assigns
///                 1403 (the first-listed id, matching the "alternating ids 1403/1404" order) to the 4-in-7
///                 share and 1404 to the 3-in-7 share -- an inference from the wording's own ordering, not a
///                 confirmed citation. A future direct re-read of Server/ts25zone/S04_MyWork03.cpp:1523-1610's
///                 own case labels for this band should confirm or correct this assignment; swapping it would
///                 only change which of two already-correct, already-legitimate ids a given draw grants, not
///                 introduce any out-of-pool id, so this assumption is low-risk in the same sense
///                 <see cref="PetBoxRewardTable" />'s own 1012-vs-1016 band-order assumption is.
///             </item>
///             <item>
///                 8-9 (2%): a previous-tribe-keyed set of three epic ids (tribe 0: 90787/90786/90788; tribe 1:
///                 90789/90790/90791; tribe 2: 90793/90792/90794), explicitly failing the request outright on an
///                 unmatched tribe -- reproduced here directly (no hardening choice needed; the legacy bulk-path
///                 table already fails closed for this band).
///             </item>
///             <item>
///                 10-11 (2%): a re-roll of sixths giving id 826 on a third of draws (2-in-6) and id 619 on
///                 two-thirds (4-in-6) -- the contract is explicit about this direction, unlike the 6-7 band.
///             </item>
///             <item>
///                 12-99 (88%): a uniform draw over 1103/1237/1166/578/579/1017/1018/1092/1093/698/696/695.
///             </item>
///         </list>
///     </para>
/// </remarks>
public static class LoyKrathongBox8108RewardTable
{
    /// <summary>world.Items id for the Loy Krathong Box itself.</summary>
    public const int BoxId = 8108;

    /// <summary>0-5 (6%) fixed reward.</summary>
    public const int FixedRewardId1407 = 1407;

    /// <summary>Roll threshold (exclusive upper bound): the 0-5 fixed band.</summary>
    public const int FixedBandThresholdExclusive = 6;

    /// <summary>Roll threshold (exclusive upper bound): the 6-7 re-roll band.</summary>
    public const int SeventhsBandThresholdExclusive = 8;

    /// <summary>
    ///     6-7 band's 4-in-7 id. See this type's own remarks: the id-to-share DIRECTION is an inference from
    ///     the contract's "alternating ids 1403/1404" wording order, not a confirmed citation.
    /// </summary>
    public const int SeventhsMajorityId = 1403;

    /// <summary>6-7 band's 3-in-7 id. Same caveat as <see cref="SeventhsMajorityId" />.</summary>
    public const int SeventhsMinorityId = 1404;

    /// <summary>Within-band re-roll denominator for the 6-7 band (sevenths).</summary>
    public const int SeventhsDenominator = 7;

    /// <summary>Within-band re-roll numerator for <see cref="SeventhsMajorityId" /> (4-in-7).</summary>
    public const int SeventhsMajorityNumerator = 4;

    /// <summary>Roll threshold (exclusive upper bound): the 8-9 tribe-keyed epic band.</summary>
    public const int EpicBandThresholdExclusive = 10;

    /// <summary>Previous-tribe-keyed epic triples (8-9 band), uniform within the selected triple.</summary>
    public static readonly FrozenDictionary<byte, ImmutableArray<int>> EpicIdsByPreviousTribe =
        new Dictionary<byte, ImmutableArray<int>>
        {
            [0] = [90787, 90786, 90788],
            [1] = [90789, 90790, 90791],
            [2] = [90793, 90792, 90794]
        }.ToFrozenDictionary();

    /// <summary>Roll threshold (exclusive upper bound): the 10-11 re-roll band.</summary>
    public const int SixthsBandThresholdExclusive = 12;

    /// <summary>10-11 band's 2-in-6 id.</summary>
    public const int SixthsMinorityId = 826;

    /// <summary>10-11 band's 4-in-6 id.</summary>
    public const int SixthsMajorityId = 619;

    /// <summary>Within-band re-roll denominator for the 10-11 band (sixths).</summary>
    public const int SixthsDenominator = 6;

    /// <summary>Within-band re-roll numerator for <see cref="SixthsMinorityId" /> (2-in-6, "a third").</summary>
    public const int SixthsMinorityNumerator = 2;

    /// <summary>The 12-99 (88%) common uniform pool.</summary>
    public static readonly ImmutableArray<int> CommonPoolIds =
        [1103, 1237, 1166, 578, 579, 1017, 1018, 1092, 1093, 698, 696, 695];

    /// <summary>
    ///     Rolls this box's full outcome for a given opener, used identically for BOTH single-open and bulk-open
    ///     dispatch in Fenrir (the legacy's own single-path/bulk-path split for this item is deliberately
    ///     collapsed into one table -- see this type's own SECURITY remarks).
    ///     <see cref="RollResult.Success" /> is false only for the 8-9 band with an unrecognized
    ///     <paramref name="previousTribe" />; every other band always resolves to a real, in-pool reward id.
    /// </summary>
    public static RollResult Roll(byte previousTribe, Random random)
    {
        var roll = random.Next(0, 100);

        if (roll < FixedBandThresholdExclusive)
            return new RollResult(true, FixedRewardId1407);

        if (roll < SeventhsBandThresholdExclusive)
        {
            var sub = random.Next(0, SeventhsDenominator);
            return new RollResult(true, sub < SeventhsMajorityNumerator ? SeventhsMajorityId : SeventhsMinorityId);
        }

        if (roll < EpicBandThresholdExclusive)
            return EpicIdsByPreviousTribe.TryGetValue(previousTribe, out var epicPool)
                ? new RollResult(true, LootBoxRewardResolver.RollUniform(random, epicPool))
                : RollResult.Failure;

        if (roll < SixthsBandThresholdExclusive)
        {
            var sub = random.Next(0, SixthsDenominator);
            return new RollResult(true, sub < SixthsMinorityNumerator ? SixthsMinorityId : SixthsMajorityId);
        }

        return new RollResult(true, LootBoxRewardResolver.RollUniform(random, CommonPoolIds));
    }

    /// <summary>Outcome of <see cref="Roll" />. RewardItemId is only meaningful when Success is true.</summary>
    public readonly record struct RollResult(bool Success, int RewardItemId)
    {
        public static RollResult Failure { get; } = new(false, 0);
    }
}
