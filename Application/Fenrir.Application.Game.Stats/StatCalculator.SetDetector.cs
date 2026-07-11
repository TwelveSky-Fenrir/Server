using System.Collections.Frozen;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    private const int EliteIdOffset = 129; // elite id = corresponding rare id + 129 (S07_MyGame03.cpp:7693-7695)
    // =========================================================================================================
    //  Sub-mechanic A of B4-set: MyUtil::ReturnSetItemValue -- the equipment "set number" detector.
    //  Ref C++: Server/ts25zone/S07_MyGame03.cpp:7628-7768 (ReturnSetItemValue full body).
    //
    //  Scope split vs. the existing set system:
    //    * NXT tribe sets (101/102/103) already live in SetBonusTables.DetectNxtSetNumber /
    //      ResolveEffectiveSetNumber and are checked there FIRST (matching legacy's own first-match-wins where
    //      NXT is evaluated before rare/legendary). So THIS detector deliberately computes ONLY the non-NXT
    //      portion -- the rare S1-S8, legendary/god/elite chain, and the mixed/count fallback, i.e. the legacy
    //      "mSetNumber" range {0,1..22,30,50}. Its result is fed to StatCalculator as the `legacySetNumber`
    //      argument, and ResolveEffectiveSetNumber overlays NXT on top (NXT wins when it matches). Net result:
    //      identical to legacy's single first-match-wins routine, just split across the two components.
    //    * Because the non-NXT range is keyed off GLOBAL item-id families (87000-89562, not tribe tables), this
    //      detector needs no previousTribe input; only NXT is tribe-keyed and that lives elsewhere.
    //
    //  Slot indices are the raw legacy equip-array indices (Server/Header/Protocol/STRUCT.h:1662-1676):
    //    amulet 0, cape 1, armor 2, gloves 3, ring 4, boots 5, null 6, weapon 7, pet 8.
    //  Only the six gear slots {0,2,3,4,5,7} are ever consulted; cape(1), null(6), pet(8), deco(9-12) never are.
    //  (The legacy FEQUIP_TYPE enum's NAMES are inverted vs. real semantics -- see EquippedItemSlot's remarks --
    //   but set detection keys off the bare index, so the naming inversion is irrelevant to the result: the id
    //   tables match whatever item physically sits in each consulted slot.)
    //
    //  Membership is ORDER-INDEPENDENT (load-bearing, S07_MyGame03.cpp:8234-8268: the LIVE CheckSetItemNumber**
    //  helpers bubble-sort their arguments before comparing sorted constant id tuples; the physically-adjacent
    //  positional-compare definitions are inside a permanently-disabled `#if 0` block, 7897-7902, never compiled).
    //  A reimplementation MUST use order-independent membership, never positional comparison.
    //
    //  OPEN QUESTION -- exact id tables not recoverable this session. The precise per-set item-id tuples each
    //  CheckSetItemNumber01..15 / the single-item legendary-piece table / the six god-tier ring+amulet
    //  disqualifier ids compare against were NOT re-opened line-by-line by the source finding (contract "Exact
    //  set-membership id tables carried forward" edge case). Per the never-invent-an-id rule they are left as
    //  EMPTY FrozenSet placeholders below, each annotated with its exact source line anchor. While empty, every
    //  id-gated branch is inert (matches nothing) and the detector produces only {0, 50, and -- via
    //  ResolveEffectiveSetNumber -- the NXT tiers}. Populating the tables from the cited anchors is a pure
    //  data-only change that activates the full {0..22,30,50} range with NO structural edit here.
    // =========================================================================================================

    // ---- consulted slot groups (bare legacy equip indices) ----
    private static readonly int[] SixGearSlots = [0, 2, 3, 4, 5, 7]; // amulet, armor, gloves, ring, boots, weapon
    private static readonly int[] RingAmuletSlots = [4, 0]; // ring + amulet
    private static readonly int[] WeaponArmorSlots = [7, 2]; // weapon + armor
    private static readonly int[] GlovesBootsArmorSlots = [3, 5, 2]; // gloves + boots + armor
    private static readonly int[] GlovesBootsSlots = [3, 5]; // gloves + boots
    private static readonly int[] WeaponBootsSlots = [7, 5]; // weapon + boots
    private static readonly int[] FourBodySlots = [7, 2, 3, 5]; // weapon + armor + gloves + boots

    // ---- rare S1-S8 direct id tables (S07_MyGame03.cpp:7652-7667 selects the result; helper id tuples below) ----
    // TODO(B4-set): confirm exact ids -- see the cited helper anchors before populating.
    private static readonly FrozenSet<int>
        Set01Ids = FrozenSet<int>.Empty; // ring+amulet -> 1  (CheckSetItemNumber01, S07_MyGame03.cpp:8234+)

    private static readonly FrozenSet<int>
        Set02Ids = FrozenSet<int>.Empty; // weapon+armor -> 2 (CheckSetItemNumber02, S07_MyGame03.cpp:8270)

    private static readonly FrozenSet<int>
        Set03Ids = FrozenSet<int>.Empty; // gloves+boots+armor -> 3 (CheckSetItemNumber03, S07_MyGame03.cpp:8328)

    private static readonly FrozenSet<int>
        Set04Ids = FrozenSet<int>.Empty; // weapon+boots -> 4 (CheckSetItemNumber04, S07_MyGame03.cpp:8363)

    private static readonly FrozenSet<int>
        Set05Ids = FrozenSet<int>
            .Empty; // all six -> 5 (CheckSetItemNumber05, S07_MyGame03.cpp:8421; alt skin ids 86816/86817/86818)

    private static readonly FrozenSet<int>
        Set06Ids = FrozenSet<int>.Empty; // ring+amulet -> 6 (CheckSetItemNumber06, S07_MyGame03.cpp:8495)

    private static readonly FrozenSet<int>
        Set07Ids = FrozenSet<int>.Empty; // weapon+armor -> 7 (CheckSetItemNumber07, S07_MyGame03.cpp:8523)

    private static readonly FrozenSet<int>
        Set08Ids = FrozenSet<int>.Empty; // gloves+boots -> 8 (CheckSetItemNumber08, S07_MyGame03.cpp:8563)

    // ---- legendary / god full-six id tables (S07_MyGame03.cpp:7668-7734) ----
    // TODO(B4-set): confirm exact ids AND confirm which CheckSetItemNumber** helper maps to which result number
    // (21/22/15) -- the helper index does not map 1:1 to the result and was flagged ambiguous in the contract.
    private static readonly FrozenSet<int>
        Set09LegendaryIds =
            FrozenSet<int>.Empty; // 4-piece legendary -> 9 (CheckSetItemNumber09, S07_MyGame03.cpp:8591)

    private static readonly FrozenSet<int>
        Set10FullSixIds = FrozenSet<int>.Empty; // full-six -> 10 (CheckSetItemNumber10, S07_MyGame03.cpp:8633)

    private static readonly FrozenSet<int>
        Set21GodFullSixIds =
            FrozenSet<int>
                .Empty; // full-six god -> 21 (god ids 88001-88024, CheckSetItemNumber13, S07_MyGame03.cpp:8728)

    private static readonly FrozenSet<int>
        Set22GodFullSixIds =
            FrozenSet<int>
                .Empty; // full-six god -> 22 (god ids 88025-88048, CheckSetItemNumber14, S07_MyGame03.cpp:8772)

    private static readonly FrozenSet<int>
        Set15FullSixIds =
            FrozenSet<int>.Empty; // full-six -> 15 (ids 89515-89562, CheckSetItemNumber15, S07_MyGame03.cpp:8816-8847)

    private static readonly FrozenSet<int>
        Set19LegendaryIds = FrozenSet<int>.Empty; // 4-piece on RAW ids -> 19 (S07_MyGame03.cpp:7717-7734)

    // ---- god-tier ring/amulet disqualifier (S07_MyGame03.cpp:7670-7671 and 7730-7733) ----
    // Six god-tier ring/amulet ids; when the ring OR amulet is one of these, the 4-piece legendary results (9
    // and 19) collapse to 0. TODO(B4-set): confirm the six ids.
    private static readonly FrozenSet<int> GodRingAmuletIds = FrozenSet<int>.Empty;

    // ---- mixed/count fallback single-item legendary-piece table (CheckSetItemNumber11 single-item, S07_MyGame03.cpp:8675) ----
    // TODO(B4-set): confirm the single-item legendary-piece ids. While empty, counter1 stays 0 and only the
    // item-sort-based counter2 (LIVE) can reach the -> 50 result.
    private static readonly FrozenSet<int> LegendaryPieceIds = FrozenSet<int>.Empty;

    /// <summary>
    ///     Detects the legacy non-NXT equipment set number for a character's currently-equipped gear -- the
    ///     value the legacy stores into <c>MyFactor.mSetNumber</c>, minus the NXT tiers which
    ///     <see cref="SetBonusTables.ResolveEffectiveSetNumber" /> overlays separately. Returns one of
    ///     <c>{0, 1..22, 30, 50}</c>: 0 means no set was detected.
    ///     <para>
    ///         First-match-wins in legacy order: rare S1-S8 -> the legendary/god/elite chain -> the mixed/count
    ///         fallback. Order-independent membership within each consulted slot group. See this file's header
    ///         for the NXT split and the (currently deferred) id-table open question -- while the id tables are
    ///         empty this method returns only 0 or 50; populating them activates the full range.
    ///     </para>
    /// </summary>
    /// <remarks>Ref C++: Server/ts25zone/S07_MyGame03.cpp:7628-7768 (MyUtil::ReturnSetItemValue).</remarks>
    public static int DetectLegacySetNumber(IReadOnlyList<EquippedItemSlot> equipment)
    {
        var bySlot = BuildSlotLookup(equipment);

        // Step 2 -- rare direct S1-S8 on RAW ids (S07_MyGame03.cpp:7652-7667).
        var rare = DetectRareSetNumber(bySlot, 0, 0);
        if (rare != 0) return rare;

        // Step 3 -- legendary / god / elite chain (only reached when no rare set matched).

        // 3a: four-piece legendary (weapon+armor+gloves+boots); god-piece disqualifier collapses it to 0.
        if (AllSlotIdsMatch(bySlot, FourBodySlots, Set09LegendaryIds))
            return HasGodRingOrAmulet(bySlot) ? 0 : 9;

        // 3b: full-six checks, in legacy order 10 -> 21 -> 22 -> 15.
        if (AllSlotIdsMatch(bySlot, SixGearSlots, Set10FullSixIds)) return 10;
        if (AllSlotIdsMatch(bySlot, SixGearSlots, Set21GodFullSixIds)) return 21;
        if (AllSlotIdsMatch(bySlot, SixGearSlots, Set22GodFullSixIds)) return 22;
        if (AllSlotIdsMatch(bySlot, SixGearSlots, Set15FullSixIds)) return 15;

        // 3c: elite tier -- re-run the rare S1-S8 checks against ids normalized by -129, yielding 11-18.
        var elite = DetectRareSetNumber(bySlot, -EliteIdOffset, 10);
        if (elite != 0) return elite;

        // 3d: four-piece legendary again on RAW ids; god-piece disqualifier collapses it to 0.
        if (AllSlotIdsMatch(bySlot, FourBodySlots, Set19LegendaryIds))
            return HasGodRingOrAmulet(bySlot) ? 0 : 19;

        // 3e: mixed/count fallback.
        return MixedCountFallback(bySlot);
    }

    /// <summary>
    ///     Runs the rare S1-S8 membership checks over the six gear slots and returns <paramref name="resultBase" />
    ///     + the rare tier (1-8), or 0 if none match. <paramref name="idOffset" /> is 0 for the raw rare pass and
    ///     -129 for the elite re-check (elite ids equal the rare id + 129, so subtracting normalizes them back).
    /// </summary>
    private static int DetectRareSetNumber(EquippedItemSlot?[] bySlot, int idOffset, int resultBase)
    {
        if (AllSlotIdsMatch(bySlot, RingAmuletSlots, Set01Ids, idOffset)) return resultBase + 1;
        if (AllSlotIdsMatch(bySlot, WeaponArmorSlots, Set02Ids, idOffset)) return resultBase + 2;
        if (AllSlotIdsMatch(bySlot, GlovesBootsArmorSlots, Set03Ids, idOffset)) return resultBase + 3;
        if (AllSlotIdsMatch(bySlot, WeaponBootsSlots, Set04Ids, idOffset)) return resultBase + 4;
        if (AllSlotIdsMatch(bySlot, SixGearSlots, Set05Ids, idOffset)) return resultBase + 5;
        if (AllSlotIdsMatch(bySlot, RingAmuletSlots, Set06Ids, idOffset)) return resultBase + 6;
        if (AllSlotIdsMatch(bySlot, WeaponArmorSlots, Set07Ids, idOffset)) return resultBase + 7;
        if (AllSlotIdsMatch(bySlot, GlovesBootsSlots, Set08Ids, idOffset)) return resultBase + 8;
        return 0;
    }

    /// <summary>
    ///     Order-independent membership test: every consulted slot must be occupied and its (offset-adjusted)
    ///     item id must be present in <paramref name="ids" />. An empty id table never matches -- that is the
    ///     inert state of an as-yet-unconfirmed set table (see file header). TODO(B4-set): confirm the exact
    ///     order-independent tuple-equality semantics (the legacy sorts both sides) alongside the ids.
    /// </summary>
    private static bool AllSlotIdsMatch(EquippedItemSlot?[] bySlot, ReadOnlySpan<int> slotIndices,
        FrozenSet<int> ids, int idOffset = 0)
    {
        if (ids.Count == 0) return false;
        foreach (var slotIndex in slotIndices)
        {
            if (bySlot[slotIndex] is not { } slot) return false;
            if (!ids.Contains(slot.Item.ItemId + idOffset)) return false;
        }

        return true;
    }

    /// <summary>Ring(4) or amulet(0) holding one of the six god-tier ring/amulet ids -- the 9/19 disqualifier.</summary>
    private static bool HasGodRingOrAmulet(EquippedItemSlot?[] bySlot)
    {
        if (GodRingAmuletIds.Count == 0) return false;
        if (bySlot[4] is { } ring && GodRingAmuletIds.Contains(ring.Item.ItemId)) return true;
        if (bySlot[0] is { } amulet && GodRingAmuletIds.Contains(amulet.Item.ItemId)) return true;
        return false;
    }

    /// <summary>
    ///     The mixed/count fallback (S07_MyGame03.cpp:7735-7765). Counter one counts gear slots whose item is a
    ///     single-item legendary piece (id table, currently deferred -> always 0). Counter two counts gear slots
    ///     whose item-sort classification is 1 or 4 -- the SAME classification sub-mechanic B (
    ///     <see
    ///         cref="ComputeG12LifeUpBonus" />
    ///     ) uses, here with opposite polarity: A increments on sort in {1,4}, B
    ///     excludes it. This port reuses the codebase's established <see cref="IsLegendary" /> (item.Sort in
    ///     {1,4}) for that classification, keeping A and B consistent. Then: counter two == 6 -> 50; else counter
    ///     one == 6 -> 20; else the two summing to 6 -> 30; else 0. (The legacy's per-slot in-struct-address
    ///     test at 7741 is always true -- a no-op artifact -- so only the slot-membership gate is modeled.)
    /// </summary>
    private static int MixedCountFallback(EquippedItemSlot?[] bySlot)
    {
        var legendaryPieceCount = 0;
        var sortCount = 0;
        foreach (var slotIndex in SixGearSlots)
        {
            if (bySlot[slotIndex] is not { } slot) continue;
            if (LegendaryPieceIds.Contains(slot.Item.ItemId)) legendaryPieceCount++;
            if (IsLegendary(slot.Item)) sortCount++;
        }

        if (sortCount == 6) return 50;
        if (legendaryPieceCount == 6) return 20;
        if (legendaryPieceCount + sortCount == 6) return 30;
        return 0;
    }
}
