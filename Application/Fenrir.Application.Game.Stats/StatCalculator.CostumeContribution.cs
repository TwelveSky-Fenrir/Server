using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

/// <summary>
///     Workstream B6 -- costume value + costume-enchant BASE-cache stat contributions. The legacy
///     <c>MyFactor</c> caches two derived members once per <c>SetAvatar</c>
///     (<c>Server/Header/Protocol/MyFactor.cpp:356-357</c>): <c>aCostumeValue</c> (four base stats resolved
///     from the worn costume item, deco-clamped and/or valid-costume-bonused) and
///     <c>aCostumeEnchantValue</c> (whose only live field in the shipped <c>ReleaseEU33</c>/V2 build is the
///     signed low byte <c>cs</c> of one enchant slot). Those two cached quantities are then read and added
///     into six BASE getters: Vitality, Ki (reads the costume's <b>Int</b>), Strength, Wisdom (reads the
///     costume's <b>Dex</b>), Critical, and Luck.
/// </summary>
/// <remarks>
///     <para>
///         <b>No longer inert.</b> Both getter wiring (<c>StatCalculator.PrimaryAttributes.cs</c>'s Vitality/
///         Strength/Ki/Wisdom getters and <c>StatCalculator.CriticalAndLuck.cs</c>'s Critical/Luck getters each
///         call the contribution helpers below) and the two derived per-character inputs this file needs are
///         now populated: (a) the resolved costume item's four raw stats -- a <c>world.Items</c> catalog
///         lookup on <c>CostumeNumber</c>, which <see cref="StatCalculator" /> cannot do itself (it holds no
///         item catalog), performed at the assembly point (<c>EquipmentService.AssembleStatContexts</c>) and
///         carried in as <see cref="CosmeticContext.CostumeValue" />; and (b) the enchant slot index + ten
///         packed slot words (<c>aCostumeIndex</c>/<c>aCostumeDate</c>), sourced from
///         <c>PlayerRuntimeState.CostumeIndex</c>/<c>PlayerRuntimeState.CostumeDate</c> and decoded by that same
///         assembly point into <see cref="CosmeticContext.CostumeEnchantCs" />. The math is additionally
///         verified directly against the cited legacy vectors (see B6 tests), independent of the
///         <see cref="ComputeBaseStats" /> end-to-end path.
///     </para>
///     <para>
///         The deco-stat id set (<c>Server/Header/function.h:2030-2072</c>: 101-151 plus the explicit ids) is
///         the <b>identical legacy list</b> already tabled as <c>CustomDecoStatItemExplicitIds</c> /
///         <see cref="IsCustomDecoStatItem" /> (<c>MyFactor.cpp:150-273</c>). It is deliberately NOT re-tabled
///         here to avoid duplicating the same ids twice; <see cref="IsDecoStatCostume" /> reuses that
///         predicate. Only the genuinely-new valid-costume set (<c>function.h:1798-2008</c>) is tabled below.
///     </para>
/// </remarks>
public static partial class StatCalculator
{
    // ---- Valid-costume id set (Server/Header/function.h:1798-2008) ----
    // Membership drives the flat +100 base-stat bonus (all four stats) and the flat +100 Luck bonus.
    // Ranges transcribed from the contract's fully-verified enumeration; 93316/93317 are commented out at
    // their first appearance but re-added at :1997-1998 (so they ARE valid); 93331/93332 (:1955-1957) and
    // 93382-93384 (:2002-2004) are commented out and therefore NOT valid (they fall in the gaps below).
    /// <summary>
    ///     The exact "valid costume" id set (<c>Server/Header/function.h:1798-2008</c>). Membership grants a
    ///     flat +100 to each of the four base costume stats and a flat +100 to base Luck.
    /// </summary>
    public static readonly FrozenSet<int> ValidCostumeIds = BuildValidCostumeIds();

    private static FrozenSet<int> BuildValidCostumeIds()
    {
        var ids = new HashSet<int>();
        AddInclusiveRange(ids, 301, 402);
        AddInclusiveRange(ids, 2146, 2148);
        AddInclusiveRange(ids, 1801, 1803);
        AddInclusiveRange(ids, 1891, 1893);
        AddInclusiveRange(ids, 17701, 17703);
        AddInclusiveRange(ids, 18124, 18132);
        AddInclusiveRange(ids, 93301, 93315);
        AddInclusiveRange(ids, 93316, 93317); // re-added at :1997-1998 -> valid
        AddInclusiveRange(ids, 93318, 93330);
        AddInclusiveRange(ids, 93334, 93345); // gap 93331-93333 excluded (93331/93332 commented out)
        AddInclusiveRange(ids, 93376, 93381);
        AddInclusiveRange(ids, 93385, 93387); // gap 93382-93384 excluded (commented out)
        AddInclusiveRange(ids, 93388, 93405);
        AddInclusiveRange(ids, 76524, 76526);
        return ids.ToFrozenSet();
    }

    private static void AddInclusiveRange(HashSet<int> set, int lo, int hi)
    {
        for (var id = lo; id <= hi; id++)
            set.Add(id);
    }

    /// <summary>True when the worn costume id is in the valid-costume set (drives the +100 base and +100 Luck).</summary>
    public static bool IsValidCostume(int costumeId)
    {
        return ValidCostumeIds.Contains(costumeId);
    }

    /// <summary>
    ///     True when the worn costume id is a "deco stat" item (clamp each base costume stat up to 100). Reuses
    ///     <see cref="IsCustomDecoStatItem" /> because <c>Server/Header/function.h:2030-2072</c> is the same
    ///     legacy id list already modelled at <c>MyFactor.cpp:150-273</c> -- see class remarks.
    /// </summary>
    public static bool IsDecoStatCostume(int costumeId)
    {
        return IsCustomDecoStatItem(costumeId);
    }

    // ---- aCostumeValue: the four cached base costume stats (Server/Header/function.h:2074-2106) ----

    /// <summary>
    ///     Resolves the cached base costume stat block from a worn costume item: zero when the item is not
    ///     found; otherwise the item's four raw stats, deco-clamped up to 100 (if a deco item), then a flat
    ///     +100 each (if a valid costume). Deco clamp is applied before the valid-costume bonus; with the
    ///     current id sets the two sets are disjoint, so at most one block fires per item.
    /// </summary>
    /// <param name="costumeId">The worn costume item id (<c>aCostumeNumber</c>).</param>
    /// <param name="itemFound">
    ///     Whether the catalog lookup on <paramref name="costumeId" /> resolved an item row. False (item not
    ///     found) yields an all-zero block with no clamp and no bonus (function.h:2076-2078).
    /// </param>
    /// <param name="itemVitality">The costume item row's Vitality field (short-widened whole number).</param>
    /// <param name="itemStrength">The costume item row's Strength field.</param>
    /// <param name="itemIntelligence">The costume item row's Intelligent field.</param>
    /// <param name="itemDexterity">The costume item row's Dexterity field.</param>
    public static CostumeBaseStatBlock ComputeCostumeBaseStatBlock(
        int costumeId,
        bool itemFound,
        int itemVitality,
        int itemStrength,
        int itemIntelligence,
        int itemDexterity)
    {
        if (!itemFound)
            return default; // all four stay zero (edge case 1)

        var vitality = itemVitality;
        var strength = itemStrength;
        var intelligence = itemIntelligence;
        var dexterity = itemDexterity;

        if (IsDecoStatCostume(costumeId)) // deco clamp-up-to-100 (edge case 2), applied before valid +100
        {
            if (vitality < 100) vitality = 100;
            if (strength < 100) strength = 100;
            if (intelligence < 100) intelligence = 100;
            if (dexterity < 100) dexterity = 100;
        }

        if (IsValidCostume(costumeId)) // flat +100 each (edge case 3)
        {
            vitality += 100;
            strength += 100;
            intelligence += 100;
            dexterity += 100;
        }

        return new CostumeBaseStatBlock(vitality, strength, intelligence, dexterity);
    }

    // ---- aCostumeEnchantValue.cs: the one live enchant field (Server/Header/function.h:2141-2163) ----

    /// <summary>
    ///     Decodes the costume-enchant magnitude <c>cs</c> from the enchant slot index and the ten packed
    ///     slot words. The gate is an index range, not an item-validity check: an index below 10 or above 19
    ///     yields zero (edge case 5). Otherwise the slot read is the index reduced modulo ten (edge case 6),
    ///     and only that word's low byte -- read as a <b>signed</b> byte, range -128..127 (edge case 7) -- is
    ///     the magnitude. All ten other enchant-power fields are zero in the shipped V2 build (edge case 10),
    ///     so they are not modelled.
    /// </summary>
    /// <param name="costumeIndex">The enchant slot index (<c>aCostumeIndex</c>); only 10-19 produce a value.</param>
    /// <param name="costumeDate">The ten packed enchant slot words (<c>aCostumeDate</c>).</param>
    public static int DecodeCostumeEnchantCs(int costumeIndex, ReadOnlySpan<int> costumeDate)
    {
        if (costumeIndex is < 10 or > 19)
            return 0; // out-of-range index -> zero enchant magnitude (edge case 5)

        var slot = costumeIndex % 10; // modulo-ten slot select (edge case 6)
        if ((uint)slot >= (uint)costumeDate.Length)
            return 0; // defensive: fewer than the expected ten slots supplied

        return ReadSignedLowByte(costumeDate[slot]);
    }

    /// <summary>
    ///     Reads the low byte of a packed word as a <b>signed</b> byte: 0-127 stay positive, 128-255 read back
    ///     negative (-128..-1). Higher bytes are ignored (<c>Server/Header/function.h:317-322</c>).
    /// </summary>
    public static int ReadSignedLowByte(int packedWord)
    {
        return (sbyte)(packedWord & 0xFF);
    }

    // ---- Per-getter contribution helpers (Server/Header/Protocol/MyFactor.cpp) ----
    // Vitality/Ki/Strength/Wisdom and the Luck cs*2 term apply the enchant magnitude by its sign with NO
    // positive guard -- a negative cs subtracts (edge case 7). Only the Critical contribution guards on a
    // positive magnitude.

    /// <summary>Base Vitality gains the cached costume Vitality plus the enchant <c>cs</c> (MyFactor.cpp:1679,:1683).</summary>
    public static int CostumeVitalityContribution(CostumeBaseStatBlock block, int cs)
    {
        return block.Vitality + cs;
    }

    /// <summary>
    ///     Base Ki gains the cached costume <b>Intelligence</b> (Ki reads the costume's Int, not a Ki field)
    ///     plus the enchant <c>cs</c> (MyFactor.cpp:1738,:1742).
    /// </summary>
    public static int CostumeKiContribution(CostumeBaseStatBlock block, int cs)
    {
        return block.Intelligence + cs;
    }

    /// <summary>Base Strength gains the cached costume Strength plus the enchant <c>cs</c> (MyFactor.cpp:1797,:1801).</summary>
    public static int CostumeStrengthContribution(CostumeBaseStatBlock block, int cs)
    {
        return block.Strength + cs;
    }

    /// <summary>
    ///     Base Wisdom gains the cached costume <b>Dexterity</b> (Wisdom reads the costume's Dex, not a Wisdom
    ///     field) plus the enchant <c>cs</c> (MyFactor.cpp:1856,:1860).
    /// </summary>
    public static int CostumeWisdomContribution(CostumeBaseStatBlock block, int cs)
    {
        return block.Dexterity + cs;
    }

    /// <summary>
    ///     Base Critical gains a costume-enchant amount driven by <c>cs</c> (MyFactor.cpp:3410-3413): a
    ///     magnitude of exactly 96 gives a flat +10 (a sentinel whose rationale is unrecoverable, OPEN
    ///     QUESTION); any other positive magnitude gives magnitude/10 as integer division (remainder dropped);
    ///     a magnitude of zero or below gives nothing.
    /// </summary>
    public static int CostumeCriticalContribution(int cs)
    {
        // OPEN QUESTION (contract edge case 8): the specific value 96 as a Critical sentinel is not
        // recoverable from the source; preserved exactly, NOT smoothed into the ordinary divide-by-ten rule.
        return cs == 96 ? 10 : cs > 0 ? cs / 10 : 0;
    }

    /// <summary>
    ///     Base Luck gains the enchant <c>cs</c> times two (no positive guard -- a negative subtracts twice),
    ///     plus a flat +100 whenever the worn costume id is in the valid-costume set (the +100 has no build
    ///     gate) (MyFactor.cpp:3665,:3667-3670).
    /// </summary>
    public static int CostumeLuckContribution(int costumeId, int cs)
    {
        return cs * 2 + (IsValidCostume(costumeId) ? 100 : 0);
    }
}

/// <summary>
///     The four cached base costume stats (<c>aCostumeValue</c>, field order Vitality/Strength/Intelligence/
///     Dexterity per <c>Server/Header/Protocol/STRUCT.h:1905-1911</c>). A <c>default</c> block is all-zero,
///     matching "worn costume item not found" (contract edge case 1). Base Ki reads
///     <see cref="Intelligence" /> and base Wisdom reads <see cref="Dexterity" /> -- see the contribution
///     helpers on <see cref="StatCalculator" />.
/// </summary>
public readonly record struct CostumeBaseStatBlock(int Vitality, int Strength, int Intelligence, int Dexterity);
