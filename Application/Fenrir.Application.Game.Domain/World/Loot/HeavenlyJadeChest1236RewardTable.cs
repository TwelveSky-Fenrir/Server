using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Reward-table DATA and roll primitive for item 1236 ("Heavenly Jade Chest" per in-code comment) --
///     workstream C10-remaining-box-pools. Like <see cref="CostumeChest76543RewardTable" />/
///     <see cref="WarlordChestRewardTable" />, this box does NOT fit any existing <see cref="BoxRewardKind" />:
///     two of its six roll branches are keyed by the opening character's own stored previous-tribe code, which
///     <see cref="BoxRewardSpec.RollRewardId(Random)" /> has no parameter to receive. This file therefore
///     supplies pure DATA plus a self-contained, fully-tested roll primitive (<see cref="Roll" />) rather than a
///     <see cref="BoxRewardSpec" />; wiring it into the shared open mechanism reuses the SAME reward-id-override
///     seam <see cref="Inventory.UseItems.Boxes.LootBoxUseItemHandler" /> already built for 76543/1378/1379 (see
///     this workstream's wiring notes), deliberately NOT made here.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ (C10-remaining-box-pools contract) : Server/ts25zone/S04_MyWork03.cpp:1441-1496 (bulk-path
///         copy, <c>#ifdef WUSE_ITEM_1236</c> -- confirmed unconditionally live in every real build,
///         Server/Header/use_inventory.h:79-93 cross-checked against Server/Header/Protocol/DEFINE.h:21-30) and
///         :5005-5057 (single-path copy) -- both re-read this session per the contract. Roll of 0-999.
///     </para>
///     <para>
///         <b>Branch layout (roll ranges are mutually exclusive and exhaustive over 0-999):</b>
///         <list type="bullet">
///             <item>
///                 Exactly 0 (0.1%): a further re-roll of 0-9 selects, respectively,
///                 <c>GetRandomCatHairBand(previousTribe)</c> = 2307 + previousTribe (1-in-10, tribe-keyed --
///                 only tribe 0/1/2 map to a real id: 2307/2308/2309), fixed id 1321 (1-in-10), or fixed id 1324
///                 (the remaining 8-in-10).
///             </item>
///             <item>1-30 (3.0%): fixed id 1007 or 1008, chosen 50/50.</item>
///             <item>
///                 31-50 (2.0%): a previous-tribe-keyed single fixed id (0-&gt;126, 1-&gt;129, 2-&gt;132).
///             </item>
///             <item>51-100 (5.0%): a further re-roll of thirds selects fixed id 601, 602, or 2249.</item>
///             <item>
///                 101-699 (59.9%): <c>GetRandomElixirNoMP()</c>, uniform over 506/508/509/578/579 (the SAME
///                 5-item pool as <see cref="PillLuckyBag1240RewardTable" />, independently duplicated here per
///                 this codebase's established per-box-file convention).
///             </item>
///             <item>700-999 (30.0%): fixed id 1045.</item>
///         </list>
///     </para>
///     <para>
///         <b>Hardening choice (this project's "harden, never reproduce" posture, not a legacy citation):</b> the
///         bulk-path copy explicitly fails the request outright on an unrecognized previous-tribe value for BOTH
///         tribe-keyed branches (the 0-roll cat-hair-band sub-branch and the 31-50 branch); the single-path copy
///         has NO such guard for either branch and would proceed to grant whatever value the reward field
///         happened to hold (for the 31-50 branch specifically: the client's own original request value, since
///         nothing in that path has written to it yet) -- the same passthrough SHAPE as item 8108's confirmed
///         exploit, gated behind an as-yet-unproven-reachable tribe precondition (every previous-tribe write
///         site found within <c>ts25zone</c> itself clamps to {0,1,2}; <c>ts25playuser</c>/avatar-creation paths
///         were not audited by the contract). <see cref="Roll" /> below always takes the SAFER of the two
///         legacy shapes -- explicit fail-closed on an unrecognized tribe for both branches, matching the
///         bulk-path's own guard -- rather than the single-path's passthrough, for both branches, regardless of
///         which physical code path (single or bulk) a given open takes in Fenrir. This is not a new invented
///         behavior: it is the bulk-path's own already-existing legacy guard, chosen over the single-path's
///         latent gap, exactly the same choice <see cref="CostumeChest76543RewardTable" />'s own remarks make
///         for the identical divergence shape on that box.
///     </para>
/// </remarks>
public static class HeavenlyJadeChest1236RewardTable
{
    /// <summary>world.Items id for the Heavenly Jade Chest itself.</summary>
    public const int BoxId = 1236;

    /// <summary><c>GetRandomCatHairBand(previousTribe)</c>'s deterministic tribe map: 2307 + previousTribe.</summary>
    public static readonly FrozenDictionary<byte, int> CatHairBandIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 2307, [1] = 2308, [2] = 2309 }.ToFrozenDictionary();

    /// <summary>Item 1321 -- the 0-roll sub-branch's 1-in-10 fixed alternative to the cat-hair band.</summary>
    public const int ZeroBranchAlternateId = 1321;

    /// <summary>Item 1324 -- the 0-roll sub-branch's 8-in-10 fixed fallback.</summary>
    public const int ZeroBranchFallbackId = 1324;

    /// <summary>The 1-30 branch's 50/50 pair.</summary>
    public static readonly ImmutableArray<int> OneToThirtyPair = [1007, 1008];

    /// <summary>The 31-50 branch's previous-tribe-keyed fixed id.</summary>
    public static readonly FrozenDictionary<byte, int> TribeFixedIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 126, [1] = 129, [2] = 132 }.ToFrozenDictionary();

    /// <summary>The 51-100 branch's uniform-thirds triple.</summary>
    public static readonly ImmutableArray<int> FiftyOneToHundredTriple = [601, 602, 2249];

    /// <summary>
    ///     <c>GetRandomElixirNoMP()</c>'s 5-entry uniform pool (same set as
    ///     <see cref="PillLuckyBag1240RewardTable.RewardItemIds" />): item 507 deliberately excluded.
    /// </summary>
    public static readonly ImmutableArray<int> ElixirNoMPPoolIds = [506, 508, 509, 578, 579];

    /// <summary>Item 1045 -- the 700-999 branch's fixed reward.</summary>
    public const int SevenHundredBranchId = 1045;

    /// <summary>
    ///     Rolls this box's full outcome for a given opener. <see cref="RollResult.Success" /> is false (no
    ///     further draw spent than already consumed) for an unrecognized <paramref name="previousTribe" /> on
    ///     either of the two tribe-keyed branches -- see this type's own remarks for why failing closed here is
    ///     a deliberate hardening choice, not a legacy citation.
    /// </summary>
    public static RollResult Roll(byte previousTribe, Random random)
    {
        var outer = random.Next(0, 1000);

        if (outer == 0)
        {
            var inner = random.Next(0, 10);
            if (inner == 0)
                return CatHairBandIdByPreviousTribe.TryGetValue(previousTribe, out var catHairBandId)
                    ? new RollResult(true, catHairBandId)
                    : RollResult.Failure;

            return inner == 1
                ? new RollResult(true, ZeroBranchAlternateId)
                : new RollResult(true, ZeroBranchFallbackId);
        }

        if (outer <= 30)
        {
            var coin = random.Next(0, 2);
            return new RollResult(true, OneToThirtyPair[coin]);
        }

        if (outer <= 50)
            return TribeFixedIdByPreviousTribe.TryGetValue(previousTribe, out var tribeFixedId)
                ? new RollResult(true, tribeFixedId)
                : RollResult.Failure;

        if (outer <= 100)
        {
            var third = random.Next(0, 3);
            return new RollResult(true, FiftyOneToHundredTriple[third]);
        }

        if (outer <= 699)
            return new RollResult(true, LootBoxRewardResolver.RollUniform(random, ElixirNoMPPoolIds));

        return new RollResult(true, SevenHundredBranchId);
    }

    /// <summary>Outcome of <see cref="Roll" />. RewardItemId is only meaningful when Success is true.</summary>
    public readonly record struct RollResult(bool Success, int RewardItemId)
    {
        public static RollResult Failure { get; } = new(false, 0);
    }
}
