using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     The per-box reward-composition data behind the op23 loot-box (chest / lucky-box) open path
///     (workstream C10): which reward table each box id rolls from, its bulk-open eligibility, and its
///     rental decoration. Built once, process-wide, immutable, read (never mutated) off the box-open path --
///     the same Domain-owned static-asset posture as <see cref="BossDropCatalog" /> (the legacy hardcodes these
///     tables in C++, so they live in memory here rather than in SQL).
/// </summary>
/// <remarks>
///     <para>
///         <b>Contract-first, never-invent posture.</b> The C10 behavior contract gives concrete, cited reward
///         ids for only a handful of boxes; for the great majority the reward pools are named-only (symbolic
///         constants / helper draws whose numeric ids live in headers not opened for that contract) or wholly
///         un-enumerated. Per this project's non-negotiable "never invent item ids" rule, ONLY the boxes whose
///         complete reward table is recoverable from the contract's own cited ranges are registered here
///         (<see cref="RegisteredBoxIds" />). Every other box in the contract falls through to the op23
///         service's clean result-1 failure exactly as before C10 -- a safe, honest "not yet populated", never
///         a fabricated grant. The full mechanism (single + bulk open, quantity clamp, placement, double-GL_606
///         audit, notice decision, pity counters) is complete, so wiring a remaining box is a data-only edit to
///         this file the day its pool is extracted. The un-registered boxes and exactly what is missing for
///         each are enumerated in this workstream's openQuestions.
///     </para>
///     <para>
///         Registered boxes and their citations:
///         <list type="bullet">
///             <item>
///                 <b>601 Mount Box</b> (Server/ts25zone/S04_MyWork03.cpp:878-983, <c>RandomMount</c>): a 0.5%
///                 (50-in-10000) rare band grants id 635, else a 0..200 banded-pool draw over five fully-cited
///                 pools.
///             </item>
///             <item>
///                 <b>7105 Mixed Fruit Lollipop</b> (Server/ts25zone/S04_MyWork03.cpp:1511-1521): a 0..100 draw,
///                 &lt;73 -&gt; 696, &lt;93 -&gt; 698, else 2397 -- modeled as a weighted table with those exact
///                 cumulative spans.
///             </item>
///             <item>
///                 <b>76542 Limited Stellar Chest</b> (Server/ts25zone/S04_MyWork03.cpp:7997-8021): a uniform
///                 pick over {76534..76538} with a 3-day rental expiry. Single-open only (not in
///                 <see cref="BulkOpenWhitelist" />).
///             </item>
///             <item>
///                 <b>602 Pet Box</b> (Server/ts25zone/S04_MyWork03.cpp:985-1095, <c>RandomPet</c>, workstream
///                 C10-petbox): a 0.80% (two 0.40% bands) special rare-pet tier, else a 0-199 banded-pool draw
///                 over five fully-cited pools -- see <see cref="PetBoxRewardTable" /> for the full table.
///             </item>
///             <item>
///                 <b>635 "15% Mount Box"</b> (Server/ts25zone/S04_MyWork03.cpp:1939-2024): a uniform 1-in-8 draw
///                 over eight tier-3 mount ids (see <see cref="MountBox635RewardTable" />). Formerly a
///                 dedicated C9-era stub (<c>MountBoxUseItemHandler</c>, deleted -- see this workstream's own
///                 wiring notes); now routed through this catalog like every other registered box.
///             </item>
///             <item>
///                 <b>2249 Cloak Box</b> (Server/ts25zone/S04_MyWork03.cpp:1097-1171, <c>RandomCloak</c>,
///                 workstream C10-cloakbox2249): a per-avatar pity counter (ceiling 100 -&gt; guaranteed 1401,
///                 zero draws consumed), else a 0.6% special band (-&gt; 1403) followed on a miss by a two-pool
///                 0-199 fallback draw -- see <see cref="CloakBoxRewardTable" />. The pity gate itself is
///                 threaded through <c>LootBoxUseItemHandler</c>'s reward-id-override hook, not this spec alone.
///             </item>
///             <item>
///                 <b>8112 Stellar Core Lucky Box</b> (Server/ts25zone/S04_MyWork03.cpp:1673-1695,7788-7821, see
///                 <see cref="StellarBox8112RewardTable" />): a seven-band weighted roll over an inclusive
///                 0..999 draw (93500..93506 at 35/20/15/12/9/6/3%). Bulk-eligible despite two misleading
///                 "Stellar boxes excluded" comments at the cited sites -- see <see cref="BulkOpenWhitelist" />'s
///                 own remark.
///             </item>
///             <item>
///                 <b>8113 Over-Enchant Box</b> (Server/ts25zone/S04_MyWork03.cpp:1697-1711,7823-7861, workstream
///                 C10-remaining-boxes): a flat uniform pick over a fixed 9-id pool, no tribe or level gate -- see
///                 <see cref="OverEnchantBox8113RewardTable" />. The one box of that workstream's four remaining
///                 items that maps onto an existing <see cref="BoxRewardKind" /> with no mechanism change.
///             </item>
///             <item>
///                 <b>1240 Pill Lucky Bag</b> (Server/ts25zone/S04_MyWork03.cpp:1498-1502,6379-6384, workstream
///                 C10-remaining-box-pools): a flat uniform pick over a fixed 5-id pool (506/508/509/578/579, id
///                 507 deliberately excluded), no tribe/pity/rental -- see
///                 <see cref="PillLuckyBag1240RewardTable" />. Both legacy copies are byte-identical.
///             </item>
///             <item>
///                 <b>8111 M15 Pet Lucky Box</b> (Server/ts25zone/S04_MyWork03.cpp:1612-1672,7726-7789, workstream
///                 C10-remaining-box-pools): a per-avatar pity counter (ceiling 200 -&gt; a 50/50 coin-flip
///                 guaranteed reward between 1012/1016, ONE draw consumed unlike box 2249's zero-draw guarantee),
///                 else a pure five-band <see cref="LootBoxRewardResolver.RollPools" /> table with no separate
///                 rare-tier pre-check -- see <see cref="M15PetLuckyBox8111RewardTable" />. The pity gate is
///                 threaded through <c>LootBoxUseItemHandler</c>'s reward-id-override hook exactly like box 2249.
///             </item>
///             <item>
///                 <b>8114</b> (no in-code display name, workstream C10-remaining-box-pools,
///                 Server/ts25zone/S04_MyWork03.cpp:7863-7917): single-open only (no bulk-path counterpart exists
///                 in the legacy source at all). Pity ceiling 200 -&gt; a DETERMINISTIC guaranteed reward (id
///                 1403, zero draws), else a pure four-band pools table -- see
///                 <see cref="CloakVariantBox8114RewardTable" />.
///             </item>
///             <item>
///                 <b>8115</b> ("15% Mount Box" per in-code comment -- an open question, possibly a copy-pasted
///                 label from the unrelated dead-code item-635 block, workstream C10-remaining-box-pools,
///                 Server/ts25zone/S04_MyWork03.cpp:7919-7995): single-open only, no bulk-path counterpart. Pity
///                 ceiling 200 -&gt; a 50/50 coin-flip guaranteed reward between 1307/1315, else a pure six-band
///                 pools table -- see <see cref="MountVariantBox8115RewardTable" />.
///             </item>
///         </list>
///     </para>
///     <para>
///         <b>Deliberately NOT registered here (workstream C10-remaining-boxes / C10-remaining-box-pools):</b>
///         76543 (Limited Costume Chest), 1378/1379 (Sky/Earth Warlord Chest), 1236 (Heavenly Jade Chest), 8005
///         (Wing Lucky Box), 8108 (Loy Krathong Box), and 720 (Chest / Reward Box) each have a fully- or mostly-
///         recovered, fully-tested reward table (<see cref="CostumeChest76543RewardTable" />,
///         <see cref="WarlordChestRewardTable" />, <see cref="HeavenlyJadeChest1236RewardTable" />,
///         <see cref="WingLuckyBox8005RewardTable" />, <see cref="LoyKrathongBox8108RewardTable" />,
///         <see cref="ChestBox720RewardTable" />) but do NOT fit any <see cref="BoxRewardKind" />: their reward
///         pool (or one band of it) is selected by the opening character's own previous-tribe code, which
///         <see cref="BoxRewardSpec.RollRewardId(Random)" /> has no parameter to receive, and 1378/1379
///         additionally gate on a G12 level precondition (<see cref="WarlordChestRewardTable.MeetsLevelGate" />)
///         the shared open mechanism never checks for any other box. Splicing these into <see cref="TryGetSpec" />
///         the way 8113/1240/8111/8114/8115 were spliced would silently grant the wrong tribe's reward to some
///         openers, so <see cref="TryGetSpec" /> deliberately still returns null for all 7 here -- unchanged by
///         the wiring described next. Item 720's reward table was, as of a fresh targeted legacy re-verification
///         pass, initially only partially recoverable (its 85% branch's <c>GetRandomAnimal5()</c> sub-pool could
///         not be located) but is now FULLY modeled for both branches -- see
///         <see cref="ChestBox720RewardTable" />'s own remarks for the complete 8-slot table and citations -- so
///         it is wired end-to-end below exactly like the other 6 tribe-keyed boxes in this list, with no
///         remaining partial-modeling caveat.
///     </para>
///     <para>
///         Workstream C10-remaining-boxes-tribe-keyed (extended by C10-remaining-box-pools): these 7 ids (76543,
///         1378, 1379, 1236, 8005, 8108, 720) are live end-to-end, dispatched entirely OUTSIDE this catalog
///         by <see cref="Inventory.UseItems.Boxes.LootBoxUseItemHandler" />'s own <c>ResolveSpec</c> (a
///         RentalDays-only placeholder <see cref="BoxRewardSpec" />, layered on top of a call to this catalog's
///         own <see cref="TryGetSpec" />) and <c>ResolveRewardIdOverride</c> (the actual previous-tribe/level2-
///         aware roll, via the same reward-id-override hook box 2249's pity counter already uses) -- see that
///         handler's own remarks for the full mechanism, including the box-8108 security hardening. This
///         catalog and <see cref="RegisteredBoxIds" /> remain untouched by that wiring; only
///         <c>Inventory.UseItems.Boxes.LootBoxUseItemHandler.HandledItemIds</c> was extended to additionally
///         claim these ids in <c>UseItemHandlerRegistry</c>.
///     </para>
/// </remarks>
public sealed class LootBoxCatalog
{
    private readonly FrozenDictionary<int, BoxRewardSpec> _byBoxId;

    private LootBoxCatalog()
    {
        var specs = new List<BoxRewardSpec>
        {
            // 601 Mount Box: 0.5% -> 635, else five banded pools over an inclusive 0..200 draw.
            BoxRewardSpec.RareBandThenPools(601,
                [new LootBoxRewardResolver.RewardBand(50, 635)],
                [
                    new LootBoxRewardResolver.RewardPool(10, [92286]),
                    new LootBoxRewardResolver.RewardPool(40, [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326]),
                    new LootBoxRewardResolver.RewardPool(100, [611, 612, 652]),
                    new LootBoxRewardResolver.RewardPool(160, [506, 507, 508, 509, 578, 579]),
                    new LootBoxRewardResolver.RewardPool(200, [1166, 1118, 1103, 1222, 1145, 1237])
                ]),

            // 602 Pet Box (workstream C10-petbox): RareBandThenPools, see PetBoxRewardTable for the full table.
            PetBoxRewardTable.Spec,

            // 635 "15% Mount Box" (C10-mountbox635): uniform 1-in-8 over eight tier-3 mount ids. Never
            // bulk-eligible (already excluded from BulkOpenWhitelist below) -- see MountBox635RewardTable's
            // own remarks for the full citation trail, including why "15%" does not describe the actual roll.
            MountBox635RewardTable.CreateSpec(),

            // 2249 Cloak Box (C10-cloakbox2249): post-pity roll only (0.6% -> 1403, else two pools over
            // 0..199). MUST stay paired with the LootBoxOpenResolver/LootBoxUseItemHandler pity-gate edits --
            // see CloakBoxRewardTable.Roll and LootBoxUseItemHandler's ResolveRewardIdOverride.
            CloakBoxRewardTable.Spec,

            // 7105 Mixed Fruit Lollipop: <73 -> 696, <93 -> 698, else 2397 (weights are those cumulative spans).
            BoxRewardSpec.Weighted(7105,
            [
                new LootBoxRewardResolver.WeightedReward(696, 73),
                new LootBoxRewardResolver.WeightedReward(698, 20),
                new LootBoxRewardResolver.WeightedReward(2397, 7)
            ]),

            // 8112 Stellar Core Lucky Box (C10-stellarbox8112): seven-band weighted roll, see
            // StellarBox8112RewardTable for the full table + citations.
            StellarBox8112RewardTable.Spec,

            // 76542 Limited Stellar Chest: uniform {76534..76538}, 3-day rental.
            BoxRewardSpec.Uniform(76542, [76534, 76535, 76536, 76537, 76538], 3),

            // 8113 Over-Enchant Box (workstream C10-remaining-boxes): uniform 9-id pool, no tribe or level
            // gate -- see OverEnchantBox8113RewardTable for the full table + citations.
            OverEnchantBox8113RewardTable.Spec,

            // 1240 Pill Lucky Bag (workstream C10-remaining-box-pools): uniform 5-id pool, no tribe/pity/rental
            // -- see PillLuckyBag1240RewardTable for the full table + citations.
            PillLuckyBag1240RewardTable.Spec,

            // 8111 M15 Pet Lucky Box (C10-remaining-box-pools): post-pity roll only (pure five-band pools, no
            // rare-tier pre-check). MUST stay paired with LootBoxUseItemHandler's ResolveRewardIdOverride edit
            // -- see M15PetLuckyBox8111RewardTable.Roll.
            M15PetLuckyBox8111RewardTable.Spec,

            // 8114 (no in-code name, C10-remaining-box-pools, single-open only): post-pity roll only
            // (deterministic 1403 guarantee, else pure four-band pools). Same pairing requirement as 8111 --
            // see CloakVariantBox8114RewardTable.Roll.
            CloakVariantBox8114RewardTable.Spec,

            // 8115 "15% Mount Box" per in-code comment (C10-remaining-box-pools, single-open only): post-pity
            // roll only (50/50 coin-flip guarantee, else pure six-band pools). Same pairing requirement as
            // 8111/8114 -- see MountVariantBox8115RewardTable.Roll.
            MountVariantBox8115RewardTable.Spec
        };

        _byBoxId = specs.ToFrozenDictionary(spec => spec.BoxId);
        RegisteredBoxIds = specs.Select(spec => spec.BoxId).ToImmutableArray();
    }

    /// <summary>The one process-wide instance, shared by every zone.</summary>
    public static LootBoxCatalog Default { get; } = new();

    /// <summary>
    ///     The box ids fully populated here -- exactly the ids the loot-box handler should be registered under in
    ///     <c>UseItemHandlerRegistry</c> (see this workstream's wiring manifest). An id absent here is not
    ///     claimed by the loot-box handler and falls through to the op23 service's clean result-1 failure.
    /// </summary>
    public ImmutableArray<int> RegisteredBoxIds { get; }

    /// <summary>
    ///     <c>IsBulkBoxNoStellar</c> whitelist (Server/ts25zone/S04_MyWork03.cpp:1291-1316): box ids eligible for
    ///     the shift+right-click bulk open. Id 635 is deliberately excluded (its own always-active single
    ///     handler is used instead). Note (observed-as-is, flagged in openQuestions): 8112 appears in this list
    ///     even though the bulk loop and the contract mark stellar boxes as intentionally excluded -- the
    ///     runtime exclusion is enforced by those boxes never being sent a bulk (value&gt;1) request, not by the
    ///     whitelist. Reproduced verbatim rather than "corrected". A whitelisted box not in
    ///     <see cref="RegisteredBoxIds" /> still bulk-opens into a no-op (each single open fails, the loop's
    ///     no-progress guard halts immediately), so an un-populated bulk box is safe, not an infinite loop.
    /// </summary>
    public static FrozenSet<int> BulkOpenWhitelist { get; } = new[]
    {
        512, 601, 602, 8112, 8113, 664, 720, 1236, 1240, 2249, 7105, 8108, 8111, 76543, 76544, 8005
    }.ToFrozenSet();

    /// <summary>
    ///     The three boxes whose success notice fires only when the reward item is elite-typed (and which write a
    ///     separate gain-item audit entry): 1035/1036/1037 (Server/ts25zone/S04_MyWork05.cpp:5408-5428). Every
    ///     other box attempts the notice unconditionally (the whitelist inside <c>NoticeForBox</c> is what
    ///     actually gates the broadcast) -- see <c>NoticeForBoxResolver</c>. None of these three is registered
    ///     yet (their reward pools are not recoverable), so this set is forward-looking scaffolding.
    /// </summary>
    public static FrozenSet<int> EliteOnlyNoticeBoxIds { get; } = new[] { 1035, 1036, 1037 }.ToFrozenSet();

    /// <summary>
    ///     <c>NoticeForBox</c>'s own reward-id whitelist (Server/ts25zone/S04_MyWork05.cpp:5089-5147): only a
    ///     reward id in this set triggers the cluster-wide elite-notice broadcast; every other reward returns
    ///     silently. Populated (workstream C10-petbox) with the two item-602 special-tier rare-pet ids
    ///     (1012, 1016) per that contract's own citation of S04_MyWork05.cpp:5089-5140/5115-5119,5147, which
    ///     confirms the special tier always notices and none of the five fallback-tier pools do. Every other
    ///     box's own notice-worthy ids remain un-enumerated (not guessed) -- flagged in openQuestions.
    /// </summary>
    public static FrozenSet<int> NoticeRewardWhitelist { get; } = new[] { 1012, 1016 }.ToFrozenSet();

    /// <summary>The reward spec for <paramref name="boxId" />, or null if this box is not populated here.</summary>
    public BoxRewardSpec? TryGetSpec(int boxId)
    {
        return _byBoxId.TryGetValue(boxId, out var spec) ? spec : null;
    }
}
