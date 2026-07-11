namespace Fenrir.Application.Game.Stats;

/// <summary>
///     Which decoration stat channel a <see cref="StatCalculator.ReturnNewStat" /> query is scored for -- the
///     legacy ReturnNewValue <c>iSort</c> selector (Server/ts25zone/GameSystem/GameSystem_02_Item.cpp:1215-1356).
///     Numeric values match the legacy codes. Selector values 5 and 7 have no branch at all (both tables leave
///     them unhandled -> 0); <see cref="AttackPower" /> (3) has no branch in either the tSort=1 or tSort=2 switch,
///     so a decoration attack-power query is a deliberate, total no-op (no case 3 in either switch).
/// </summary>
public enum DecorationStatKind
{
    /// <summary>1 -- Max HP / life.</summary>
    MaxLife = 1,

    /// <summary>2 -- Max MP / mana (tSort=2 / IZ octet contributes nothing for this selector).</summary>
    MaxMana = 2,

    /// <summary>3 -- attack power: a genuine no-op, no table branch in either switch -> always 0.</summary>
    AttackPower = 3,

    /// <summary>4 -- defense power.</summary>
    DefensePower = 4,

    /// <summary>6 -- attack block / dodge (tSort=2 / IZ octet contributes nothing for this selector).</summary>
    AttackBlock = 6,

    /// <summary>8 -- element defense power.</summary>
    ElementDefensePower = 8
}

public static partial class StatCalculator
{
    // ================================================================================================
    //  Workstream B3-deco -- per-slot equipment stat ramps and decoration ("deco sort==2") octet tables.
    //
    //  Two independent pure value engines the MyFactor stat-aggregation routines fold into their running
    //  totals while walking a character's equipped slots. No I/O, no packet, no DB, no shared-memory write --
    //  every input is a plain value the caller already has in memory. Both are added into in-memory
    //  accumulators (attack power, defense power, hit, dodge, element attack, element defense, Max HP/MP);
    //  persistence/broadcast of the resulting totals happens elsewhere.
    //
    //  Ref. C++ :
    //   - Server/ts25zone/S07_MyGame03.cpp:1134-1341 -- MyUtil::ReturnIUEffectValue (the LIVE definition, all
    //     effect sorts 1-6): the shared three-band level ramp (cutoffs 100/113/146, offsets 0.00/6.00/8.00,
    //     steps 0.10/0.20/0.50) and the fall-through return 0 at :1340. Per-(sort,category) base/K at
    //     :1166-1226 (sort 2), :1227-1267 (sort 3), :1268-1300 (sort 4), :1301-1319 (sort 5), :1320-1338
    //     (sort 6). Two other copies (CUTIL::ReturnIUEffectValue in the "- Copy"/"_OLD_FORMULAR" files) are
    //     dead code excluded from the zone build -- NOT implemented from; see openQuestions.
    //   - Server/Header/function.h:317-343 -- Return{IS,IU,IM,IZ}Value: each copies one byte of the packed
    //     upgrade value through a signed char (octet 0=IS, 1=IU, 2=IM, 3=IZ), so a stored byte 128-255 reads
    //     back NEGATIVE. Same signed-octet decode as RuneStatDecoder in this project.
    //   - Server/ts25zone/GameSystem/GameSystem_02_Item.cpp:1215-1356 -- ITEMSYSTEM::ReturnNewValue: result
    //     initialised to 0 (unhandled selectors return 0); tSort=1 disjoint-band table for selectors 1/2/4/6/8
    //     (:1219-1249); tSort=2 decoration table (:1250-1354). No branch for selector 3 in either switch.
    //   - Server/ts25zone/GameSystem/GameSystem_02_Item.cpp:1358-1368 -- ITEMSYSTEM::ReturnNewStat: zero-value
    //     short-circuit, three low octets (IS/IU/IM) via tSort=1 and the high octet (IZ) via tSort=2, returns
    //     the four-octet sum.
    //   - Server/ts25zone/S07_MyGame03.cpp:7484-7513 -- MyUtil::ReturnItemSort: returns 2 ("deco") exactly when
    //     item type is 5 and equip-info category is 11/12/13/14; the gate on every decoration ReturnNewStat call.
    //   - Server/ts25zone/S07_MyGame03.cpp:1342-1345,1477-1488 -- ReturnISValueWithLevelLimit /
    //     ReturnIUValueWithLevelLimit are identity functions in the shipped code (old cap tables dead-commented):
    //     the IU point-count multiplier is NOT level-capped anywhere on this path.
    //
    //  NOTE ON effect sort 1: also reproduced here (for completeness of the general engine), but the LIVE
    //  weapon-attack wiring already uses the standalone WeaponAttackEffectValue in StatCalculator.AttackPower.cs.
    //  The only behavioural difference is that this general function returns 0 at item level >= 146 (per the
    //  legacy fall-through), whereas WeaponAttackEffectValue does not guard >= 146 (real world.Items levels are
    //  1..145). Migrating sort 1 onto this function is OPTIONAL and out of this slice's scope -- see wiringManifest.
    //
    //  NOTE ON octet -> slot-field mapping: this engine is packed-int / octet based and takes NO EquippedItemSlot
    //  dependency, so the (which EquippedItemSlot byte is which octet) question stayed entirely in the wiring
    //  pass. RESOLVED (wiring pass, re-verified B3-octet-mapping contract, scratchpad/contracts/wave8/
    //  B3-octet-mapping.md): for decoration items specifically, ONLY IS (Enchant) has a cited legacy source --
    //  see DecorationStatContribution's own remarks below for the full IU/IM/IZ justification. The original
    //  "PackDecorationUpgradeOctets(Enchant, Combine, Refine, Socket)" 4-field assumption was wrong on two
    //  counts: (1) decoration items never decode IU/IM at all per S04_MyWork02.cpp:218-259, and (2) "Socket"
    //  is not IZ under any legacy path -- IZ belongs to an unrelated Rune Stone mechanic and otherwise has no
    //  confirmed gameplay identity for ordinary equip items. The engine itself (ReturnNewStat/PackDecoration-
    //  UpgradeOctets) is unchanged and still correct for whatever octet order a caller packs; only the wiring
    //  helper's choice of which EquippedItemSlot fields to feed it was corrected.
    // ================================================================================================

    // ------------------------------------------------------------------------------------------------
    //  ReturnIUEffectValue -- per-point value from the item-template row (effect sorts 1-6)
    // ------------------------------------------------------------------------------------------------

    /// <summary>
    ///     The per-point stat contribution for one equipped item, computed as the integer truncation toward
    ///     zero of <c>base + r(level) * K</c>, where <c>base</c>/<c>K</c> are fixed per (effect sort, category
    ///     code) and <c>r(level)</c> is a single piecewise-linear ramp shared byte-for-byte across every case
    ///     (S07_MyGame03.cpp:1134-1341). Returns 0 for any effect sort outside 1-6, any category code not listed
    ///     for the given sort, and any item level at or above 146 (no band matches). No clamp is applied: an item
    ///     level below 45 yields a negative <c>r</c> and a slightly-below-<c>base</c> truncated value, reproduced
    ///     verbatim (a value quirk, not a failure).
    /// </summary>
    /// <param name="effectSort">
    ///     Stat channel selector: 1 = attack power, 2 = defense power, 3 = attack hit/success, 4 = attack
    ///     block/dodge, 5 = element attack power, 6 = element defense power. Any other value yields 0.
    /// </param>
    /// <param name="itemSort">The item-template equipment-category code (<c>iSort</c>). Eligibility is per sort.</param>
    /// <param name="itemLevel">The item-template level column (<c>iLevel</c>) -- the item's own level, not the character's.</param>
    public static int ReturnIUEffectValue(int effectSort, int itemSort, int itemLevel)
    {
        if (ResolveIuEffectCoefficients(effectSort, itemSort) is not { } coefficients)
            return 0;

        // Level 146 and above matches no band -> 0 (S07_MyGame03.cpp:1340). Checked first so the ramp switch's
        // final arm only ever covers the 113..145 band.
        if (itemLevel >= 146)
            return 0;

        var level = (float)itemLevel;
        var (rampBase, pivot, step) = level switch
        {
            < 100f => (0f, 45f, 0.10f), // r = (level - 45) * 0.10  (0 at 45, 5.4 at 99)
            < 113f => (6f, 100f, 0.20f), // r = 6.00 + (level - 100) * 0.20  (6.0 at 100, 8.4 at 112)
            _ => (8f, 113f, 0.50f) // r = 8.00 + (level - 113) * 0.50  (8.0 at 113, 24.0 at 145)
        };

        var r = rampBase + (level - pivot) * step;
        return (int)(coefficients.Base + r * coefficients.K);
    }

    /// <summary>
    ///     The per-slot contribution folded into a running stat total at a ReturnIUEffectValue call site: the
    ///     per-point value (<see cref="ReturnIUEffectValue" />) multiplied by the slot's IU point count. The
    ///     legacy "with level limit" wrapper on that count is an identity in the shipped build (no cap), so the
    ///     count is passed through unchanged (S07_MyGame03.cpp:1477-1488).
    /// </summary>
    /// <param name="iuPointCount">
    ///     The IU point count -- the second (IU) octet of the slot's packed upgrade value, a signed byte
    ///     (-128..127). The caller supplies the already-decoded value.
    /// </param>
    public static int IUEffectSlotContribution(int effectSort, int itemSort, int itemLevel, int iuPointCount)
    {
        return ReturnIUEffectValue(effectSort, itemSort, itemLevel) * iuPointCount;
    }

    /// <summary>
    ///     The fixed (base, per-ramp-point slope K) pair for one (effect sort, category code) pair, or null when
    ///     the sort/category pair has no branch (contributes 0). Values are the source literals reproduced from
    ///     S07_MyGame03.cpp:1166-1338 (sorts 2-6) and :1160-1165 (sort 1, for completeness).
    /// </summary>
    private static (float Base, float K)? ResolveIuEffectCoefficients(int effectSort, int itemSort)
    {
        return (effectSort, itemSort) switch
        {
            // sort 1 -- attack power (weapon): categories 4 and 13-21
            (1, 4 or >= 13 and <= 21) => (14.34f, 0.72f),

            // sort 2 -- defense power
            (2, 8) => (2.00f, 0.10f),
            (2, 9) => (6.36f, 0.32f),
            (2, 10) => (1.82f, 0.09f),
            (2, 12) => (0.91f, 0.05f),

            // sort 3 -- attack hit/success
            (3, 10) => (13.36f, 0.67f),
            (3, >= 13 and <= 21) => (5.73f, 0.29f),

            // sort 4 -- attack block/dodge
            (4, 9) => (0.95f, 0.05f),
            (4, 12) => (2.23f, 0.11f),

            // sort 5 -- element attack power
            (5, 11) => (2.00f, 0.26f),

            // sort 6 -- element defense power
            (6, 7) => (1.00f, 0.13f),

            _ => null
        };
    }

    // ------------------------------------------------------------------------------------------------
    //  Decoration ramp -- ReturnNewStat / ReturnNewValue octet tables (deco "ReturnItemSort==2")
    // ------------------------------------------------------------------------------------------------

    /// <summary>
    ///     The "deco sort==2" classification gate: an item is treated as a decoration exactly when its item type
    ///     is 5 and its equip-info category value is one of 11, 12, 13, or 14 (MyUtil::ReturnItemSort returning 2,
    ///     S07_MyGame03.cpp:7484-7513). Only such items feed <see cref="ReturnNewStat" />, and only at equipped
    ///     decoration slots (array indices 9 and above).
    /// </summary>
    /// <param name="equipInfoCategory">
    ///     The item template's SECOND equip-info value, not the first -- both the equip-slot selector and this
    ///     decoration-eligibility test read <c>iEquipInfo[1]</c> (the wiring pass's re-verified
    ///     B3-octet-mapping contract, Edge cases: "the equip-slot selector and the deco-eligibility gate both
    ///     use the second, not the first, of an item template's two-value equip-info field",
    ///     S07_MyGame03.cpp:7477-7513). Callers must pass <c>ItemRowDto.EquipInfo2</c>, never EquipInfo1.
    /// </param>
    public static bool IsDecorationItem(int itemType, int equipInfoCategory)
    {
        return itemType == 5 && equipInfoCategory is 11 or 12 or 13 or 14;
    }

    /// <summary>
    ///     B3-deco wiring helper: the total decoration <see cref="ReturnNewStat" /> contribution across every
    ///     occupied decoration slot (array indices 9-12) for one stat channel.
    ///     <para>
    ///         ONLY the IS (Enchant/"Improve") octet is packed -- IU/IM/IZ are deliberately left at 0, not
    ///         guessed. The fresh B3-octet-mapping re-verification
    ///         (scratchpad/contracts/wave8/B3-octet-mapping.md, re-opened specifically to correct the earlier
    ///         "IZ=Socket" assumption) states plainly, twice (Outputs bullet 1 AND Edge cases): IS is "the only
    ///         octet decoded for decoration-category items" in every legacy path that contract's session could
    ///         find (S04_MyWork02.cpp:218-259, <c>ITEM_VALUE_FACTOR</c>). Concretely:
    ///         <list type="bullet">
    ///             <item>
    ///                 IU (Combine) is confirmed as "Combine" ONLY for the weapon/armor/accessory category --
    ///                 the same contract states IU is "never decoded for decoration-category items". Even though
    ///                 effect-sort 1-6 wiring elsewhere in this file legitimately reads
    ///                 <c>EquippedItemSlot.Combine</c> for non-decoration slots, doing the same for a decoration
    ///                 slot's IU octet has no cited legacy source.
    ///             </item>
    ///             <item>
    ///                 IM (Refine) is decoded only for the same non-decoration category, AND its one write
    ///                 path (SMELT_ITEM_SEND) is confirmed dead in every real build -- doubly unjustified here.
    ///             </item>
    ///             <item>
    ///                 IZ has NO confirmed gameplay identity for any ordinary equip item (weapon, armor,
    ///                 accessory, OR decoration) anywhere in the source tree; it belongs to a wholly separate Rune
    ///                 Stone mechanic (STR/DEX/VIT/INT overwrite, ProcessForInventoryToInventoryRune,
    ///                 S04_MyWork05.cpp:898-1127) that has nothing to do with decoration items. It is emphatically
    ///                 NOT "Socket" -- Socket is its own disjoint 3-value-per-slot array, never derived from this
    ///                 packed value under any legacy path.
    ///             </item>
    ///         </list>
    ///         TODO(cpp-zone-gameplay-analyst): revisit if a legacy citation for a decoration-item IU/IM/IZ
    ///         contribution ever surfaces (open question #2 in that contract also flags EquipInfo1-vs-2, which
    ///         this helper's caller resolves via <see cref="IsDecorationItem" />'s own doc).
    ///     </para>
    /// </summary>
    public static int DecorationStatContribution(DecorationStatKind stat, EquippedItemSlot?[] bySlot)
    {
        var total = 0;
        for (var d = 9; d <= 12; d++)
        {
            if (bySlot[d] is not { } deco) continue;
            if (!IsDecorationItem(deco.Item.Type, deco.Item.EquipInfo2)) continue;

            var decoPacked = PackDecorationUpgradeOctets(deco.Enchant, 0, 0, 0); // IS only -- see remarks above
            total += ReturnNewStat(stat, decoPacked);
        }

        return total;
    }

    /// <summary>
    ///     The total decoration contribution for one decoration slot and one stat channel: the sum of four octet
    ///     sub-scores (GameSystem_02_Item.cpp:1358-1368). The three low octets (IS, IU, IM) are each scored
    ///     through the <c>tSort=1</c> table; the high octet (IZ) through the <c>tSort=2</c> table. Returns 0
    ///     immediately when the whole packed value is zero, before any octet decoding. Each octet is decoded as a
    ///     signed byte in raw memory order (least-significant first), so a stored byte 128-255 decodes negative,
    ///     falls outside every legal band, and contributes 0 (function.h:317-343).
    /// </summary>
    /// <param name="stat">The decoration stat channel (see <see cref="DecorationStatKind" />).</param>
    /// <param name="packedUpgradeValue">
    ///     The slot's four-byte packed upgrade value. Octet order: bit 0-7 = IS, 8-15 = IU, 16-23 = IM,
    ///     24-31 = IZ (build with <see cref="PackDecorationUpgradeOctets" />).
    /// </param>
    public static int ReturnNewStat(DecorationStatKind stat, int packedUpgradeValue)
    {
        if (packedUpgradeValue == 0)
            return 0;

        var octetIs = (sbyte)packedUpgradeValue;
        var octetIu = (sbyte)(packedUpgradeValue >> 8);
        var octetIm = (sbyte)(packedUpgradeValue >> 16);
        var octetIz = (sbyte)(packedUpgradeValue >> 24);

        return ReturnNewValue(1, stat, octetIs)
               + ReturnNewValue(1, stat, octetIu)
               + ReturnNewValue(1, stat, octetIm)
               + ReturnNewValue(2, stat, octetIz);
    }

    /// <summary>
    ///     Scores one decoration octet through one of the two decoration value tables
    ///     (ITEMSYSTEM::ReturnNewValue, GameSystem_02_Item.cpp:1215-1356). <paramref name="tableSort" /> selects
    ///     the table: 1 = the disjoint-band table for the low octets (IS/IU/IM), 2 = the decoration "sort==2"
    ///     ramp for the high octet (IZ). Any octet outside the selected table's legal band for
    ///     <paramref name="stat" /> yields 0.
    /// </summary>
    public static int ReturnNewValue(int tableSort, DecorationStatKind stat, int octet)
    {
        return tableSort switch
        {
            1 => ReturnNewValueLowOctet(stat, octet),
            2 => ReturnNewValueHighOctet(stat, octet),
            _ => 0
        };
    }

    /// <summary>
    ///     Reconstructs a packed upgrade value from its four signed-byte octets in raw memory order (IS lowest,
    ///     IZ highest) -- the inverse of the <see cref="ReturnNewStat" /> decode. Provided so the wiring pass and
    ///     tests express octet order explicitly rather than open-coding shifts.
    /// </summary>
    public static int PackDecorationUpgradeOctets(int octetIs, int octetIu, int octetIm, int octetIz)
    {
        return (byte)(sbyte)octetIs
               | ((byte)(sbyte)octetIu << 8)
               | ((byte)(sbyte)octetIm << 16)
               | ((byte)(sbyte)octetIz << 24);
    }

    // tSort=1 -- disjoint-band table applied to the three low octets (IS/IU/IM). Each stat occupies its own
    // octet-value band so three octets can carry three independent stats without collision
    // (GameSystem_02_Item.cpp:1219-1249). Out-of-band (including any negative/signed-wrapped octet) -> 0.
    private static int ReturnNewValueLowOctet(DecorationStatKind stat, int octet)
    {
        return stat switch
        {
            DecorationStatKind.MaxLife => octet is >= 41 and <= 60 ? 100 * (octet - 40) : 0, // 100..2000
            DecorationStatKind.MaxMana => octet is >= 61 and <= 80 ? 125 * (octet - 60) : 0, // 125..2500
            DecorationStatKind.DefensePower => octet is >= 1 and <= 20 ? 50 * octet : 0, // 50..1000
            DecorationStatKind.AttackBlock => octet is >= 21 and <= 40 ? 20 * (octet - 20) : 0, // 20..400
            DecorationStatKind.ElementDefensePower => octet is >= 81 and <= 100 ? 50 * (octet - 80) : 0, // 50..1000
            _ => 0 // selector 3 (AttackPower) and any unlisted selector: no branch
        };
    }

    // tSort=2 -- the decoration "sort==2" ramp applied to the high octet (IZ) only
    // (GameSystem_02_Item.cpp:1250-1354). MaxMana and AttackBlock are unconditional 0 here.
    private static int ReturnNewValueHighOctet(DecorationStatKind stat, int octet)
    {
        return stat switch
        {
            DecorationStatKind.MaxLife => MaxLifeHighOctetTier(octet),
            DecorationStatKind.MaxMana => 0, // unconditional 0 (:1299-1301)
            DecorationStatKind.DefensePower => octet is >= 1 and <= 25 ? ModuloFiveMap(octet) : 0, // (:1302-1325)
            DecorationStatKind.AttackBlock => 0, // unconditional 0 (:1326-1328)
            DecorationStatKind.ElementDefensePower => octet is >= 26 and <= 50
                ? ModuloFiveMap(octet)
                : 0, // (:1329-1352)
            _ => 0 // selector 3 (AttackPower) and any unlisted selector: no branch
        };
    }

    // Max-HP IZ ramp: tiered by 5 with the band REPEATING at 26 (GameSystem_02_Item.cpp:1254-1298). Anything
    // outside 1-50 (including the negative/signed-wrapped case) -> 0.
    private static int MaxLifeHighOctetTier(int octet)
    {
        return octet switch
        {
            >= 1 and <= 5 or >= 26 and <= 30 => 400,
            >= 6 and <= 10 or >= 31 and <= 35 => 800,
            >= 11 and <= 15 or >= 36 and <= 40 => 1200,
            >= 16 and <= 20 or >= 41 and <= 45 => 1600,
            >= 21 and <= 25 or >= 46 and <= 50 => 2000,
            _ => 0
        };
    }

    // Shared IZ modulo-5 map for DefensePower (gated 1-25) and ElementDefensePower (gated 26-50). Magnitude
    // beyond the gate is ignored; only the residue matters (GameSystem_02_Item.cpp:1302-1352). Callers gate the
    // octet range first, so only positive octets 1-50 reach here.
    private static int ModuloFiveMap(int octet)
    {
        return (octet % 5) switch
        {
            1 => 200,
            2 => 400,
            3 => 600,
            4 => 800,
            0 => 1000,
            _ => 0 // unreachable for the gated 1-50 range; keeps the int switch total
        };
    }
}
