using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Item 602 "Pet Box" reward-pool data (workstream C10-petbox). Extends <see cref="LootBoxCatalog" /> with
///     the one box its own remarks flagged as unregistered pending this pool's data ("602 Pet Box is in the
///     bulk whitelist but its fallback pools are not recoverable -&gt; deliberately absent"). Same
///     <see cref="BoxRewardKind.RareBandThenPools" /> shape the 601 Mount Box entry already uses -- one shared
///     0-9999 draw against a list of rare bands (first hit wins), falling through on a miss to a second,
///     independent 0-199 draw across five banded pools (<see cref="LootBoxRewardResolver.RollRareBandThenPools" />)
///     -- so this file is data-only: it does not duplicate the roll/placement mechanism, it only supplies the
///     table plus the ready-built <see cref="Spec" /> for <see cref="LootBoxCatalog" /> to register (see this
///     slice's wiring manifest -- <c>LootBoxCatalog.cs</c> itself is not edited here per the concurrent-wave
///     "new files only" rule).
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:985-1095 (<c>RandomPet</c>) -- special-tier roll and
///         even split :998-1010 (0-9999 draw, bottom 80 = 0.80% total hits the special tier, mutually
///         exclusive with the fallback pool: the branch returns unconditionally at :1018-1027 without ever
///         consulting the fallback table) ; fallback-tier roll and five-band partition :1030-1069 (a second,
///         independent 0-199 draw) ; in-band uniform item pick :1077 ; reward-lookup guard :1079-1084 (shared
///         with :1011-1016 for the special tier).
///     </para>
///     <para>
///         <b>Special tier (2 rare-pet ids, 0.40% each, 0.80% combined).</b> Both concrete ids -- 1012 and
///         1016 -- are confirmed as this pool's own special-tier members by the C10-petbox contract's Open
///         Questions section, which enumerates every recoverable id in item 602's pool grouped in roll order;
///         1012 and 1016 are the only two ids in that enumeration ahead of the single-item first fallback
///         band.
///         <b>
///             Which of the two fills the "bottom half" (draw 0-39) vs. "top half" (draw 40-79) band is
///             NOT separately stated by the contract
///         </b>
///         (no C++ control-flow detail was carried into it, per the
///         zero-code rule) -- assigned here in ascending id order (1012 = bottom, 1016 = top) as the only
///         non-arbitrary tie-break available, not a recovered fact. Flagged in this slice's openQuestions;
///         swapping the two only changes which of two already-correct ids a given draw range grants, it does
///         not change any probability, so this assumption is low-risk but still an assumption. Réf.
///         Server/ts25zone/S04_MyWork03.cpp:998-1010; display names for 1012-1016 are absent from every
///         Server/ file checked (only their sequential 8212-8216 id-remap targets exist, no name comment) --
///         Server/ts25ztool/data_translate_item.h:2200-2214ish (remap range, confirmed no name comment).
///     </para>
///     <para>
///         <b>Fallback pool (99.20% combined, five bands over an inclusive 0-199 draw).</b> Ceilings below are
///         each band's cumulative upper bound (matching <see cref="LootBoxRewardResolver.RollPools" />'s "first
///         pool whose ceiling is at or above the draw wins" semantics), chosen so each band's width
///         (ceiling - previous ceiling) reproduces the C10-petbox contract's own band-width table exactly
///         (21/40/60/60/19, summing to the full 200-value 0-199 range):
///         <list type="bullet">
///             <item>Band 1, ceiling 20 (width 21, 10.416% absolute): single fixed item 1178.</item>
///             <item>Band 2, ceiling 60 (width 40, 19.84% absolute): four items 1002/1003/1004/1005, 4.96% each.</item>
///             <item>Band 3, ceiling 120 (width 60, 29.76% absolute): three items 1190/1491/1492, 9.92% each.</item>
///             <item>
///                 Band 4, ceiling 180 (width 60, 29.76% absolute): six items 506/507/508/509/578/579
///                 (consumable-potion family), 4.96% each.
///             </item>
///             <item>
///                 Band 5, ceiling 199 (width 19, 9.424% absolute): six items 1103/1118/1145/1166/1222/1237
///                 (charm/scroll family), 1.5707% each.
///             </item>
///         </list>
///         All eighteen fallback-tier ids plus the two special-tier ids above are exactly the twenty ids the
///         contract's Open Questions section lists as this pool's full recoverable id set. Réf.
///         Server/ts25zone/S04_MyWork03.cpp:1030-1069,1077; name cross-references
///         Server/Header/itemsort99.h:41,70-97,132,183,211,217,257,276,277 (1178, 506-509/578-579, 1103, 1118,
///         1145 unnamed there, 1166, 1190, 1491, 1492) and Server/ts25zone/S04_MyWork03.cpp:2880-2907,5060
///         (mirrored potion/elixir names, 1237 case-site comment); item 1002's own name comment exists in the
///         same four-item remap block as 1003/1004/1005 in
///         Server/ts25ztool/data_translate_item.h:2248,2258,2260,2265, but 1003/1004/1005 themselves carry no
///         name comment there.
///     </para>
///     <para>
///         <b>Not modeled here (open questions, carried forward, not invented):</b> the data-driven
///         <c>ITEM_INFO::iSort</c>/<c>iType</c> per reward id (drives stack-vs-unique placement, exact placed
///         quantity, and serial eligibility) is not present anywhere in <c>Server/</c> for any of these twenty
///         ids -- the branching mechanism itself is already fully implemented generically by
///         <c>LootBoxOpenResolver</c>/<c>BoxRewardPlacementResolver</c> reading Fenrir's own <c>world.Items</c>
///         seed data, so this is a data-seeding gap, not a missing mechanism. No pity/guarantee counter exists
///         for this box (confirmed by <c>RandomPet</c>'s own body containing no such counter, unlike several
///         sibling boxes) and no faction/tribe dependence exists either (confirmed by absence of any such read
///         in the same function body) -- both already correctly un-modeled by this table (no counter field,
///         no faction branch).
///     </para>
/// </remarks>
public static class PetBoxRewardTable
{
    /// <summary>The box's world.Items id (the item sitting in the opened inventory slot).</summary>
    public const int BoxId = 602;

    /// <summary>
    ///     The two special-tier rare-pet rare bands (0.40% each, 0.80% combined) -- first hit, in list order,
    ///     wins. See this type's own remarks for the bottom/top-half id-assignment caveat.
    /// </summary>
    public static readonly ImmutableArray<LootBoxRewardResolver.RewardBand> RareBands =
    [
        new(40, 1012),
        new(40, 1016)
    ];

    /// <summary>The five fallback-tier banded pools over the independent inclusive 0-199 draw.</summary>
    public static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> Pools =
    [
        new(20, [1178]),
        new(60, [1002, 1003, 1004, 1005]),
        new(120, [1190, 1491, 1492]),
        new(180, [506, 507, 508, 509, 578, 579]),
        new(199, [1103, 1118, 1145, 1166, 1222, 1237])
    ];

    /// <summary>
    ///     The ready-to-register <see cref="BoxRewardSpec" /> for box 602. Add this to
    ///     <see cref="LootBoxCatalog" />'s <c>specs</c> list (see this slice's wiring manifest) to complete
    ///     registration -- no other file needs a change, since <c>RegisteredBoxIds</c>,
    ///     <c>LootBoxUseItemHandler.HandledItemIds</c>, and <c>UseItemHandlerRegistry</c>'s constructor loop all
    ///     derive from that list automatically.
    /// </summary>
    public static readonly BoxRewardSpec Spec = BoxRewardSpec.RareBandThenPools(BoxId, RareBands, Pools);
}
