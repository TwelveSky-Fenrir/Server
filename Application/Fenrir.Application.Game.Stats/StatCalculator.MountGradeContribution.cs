using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ================================================================================================
    // Mount / animal grade stat contributions (WORKSTREAM B8-mount, deepened by wave11's B8-myanimal-table,
    // and by workstream mount-grade-contribution-table which recovered the base table's own row VALUES)
    //
    // Legacy MyFactor folds a mounted avatar's mount state into its derived-stat recompute. In Fenrir terms
    // that is the BASE cache layer (the SetAvatar / factor rebuild that ComputeBaseStats models), NOT the
    // per-resolution effective layer -- every tier below is applied inside a GetBase* stat function, so it
    // belongs in a ComputeBaseStats getter. (This corrects MountContext's B1 doc, which tentatively annotated
    // the runtime-attribute half as EFFECTIVE; the B8 contract's Trigger places the whole pass in the factor
    // rebuild = base recompute.)
    //
    // The contract describes four tiers, applied in this order inside each stat's function:
    //   Tier 0  -- mount decode (once, before the stat blocks; MyAnimal.cpp:190-227). Two independent halves:
    //              (a) the base-table row lookup by item id (per-stat tier columns 0/5/10/15/20, absorb,
    //              model, ability-effect) -- NOW modeled below as MountBaseRow/MountBaseDataByItemId/
    //              TryGetMountBaseRow: the full 94-row mANIMAL_DATA catalogue (not the earlier-reported 74
    //              rows), recovered in full by workstream mount-grade-contribution-table. Wiring the
    //              resolved row's columns into the per-stat GetBase* call sites (ComputeMaxLife et al.) is a
    //              separate integration step, not done here -- see each getter's own "mount grade ...
    //              blocked" remark and this workstream's openQuestions; (b) the per-digit power decode --
    //              NOW modeled below as DecodeMountPowerDigits/ComputeMountFlatBonuses, which the wave11
    //              contract newly pinned: the fixed digit-place -> stat mapping (previously ambiguous, see
    //              this workstream's own memory) and the "activity strictly positive" gate on the decode
    //              itself, not just on each flat-bonus call site. Sourcing the raw (power, activity) pair
    //              from PlayerRuntimeState into a call site is still unwired -- see openQuestions.
    //   Tier 1  -- grade percentage multiplier (per derived stat): a base-table column value of 5/10/15/20
    //              multiplies the running stat total by 1.05/1.10/1.15/1.20, single float multiply then
    //              truncate toward zero. Below: ApplyMountGradeMultiplier{FourTier,ThreeTier}. Confirmed
    //              directly against the now-recovered 94-row table: the three-tier stats hit/dodge/
    //              element-attack/element-defense never see column 20 anywhere in the table's own data (not
    //              a code gap); critical's own column, however, is NOT uniformly zero -- it is nonzero
    //              (5/10/15) for 18 of the 94 rows (the whole Deer and Wolf families, plus the two Christmas
    //              event ids 510/511), so ApplyMountGradeMultiplierThreeTier is genuinely exercised for
    //              critical too, correcting an earlier pass's "critical never populated" assumption for this
    //              column specifically (Tier 2 still has no separate flat critical additive -- that part is
    //              unaffected, see MountFlatBonusPerPointByStat).
    //   Tier 2  -- flat per-point additive (per derived stat): a fixed multiple of the decoded grade digit,
    //              added AFTER the Tier-1 multiplier so it is never scaled by the percentage. Below:
    //              MountFlat*Bonus (primitives) / DecodeMountPowerDigits+ComputeMountFlatBonuses (the decode
    //              + one-call composition wave11 added). Per-point magnitudes re-verified unchanged.
    //   Tier 2b -- absorb -> each of the 4 primary stats (Vit/Ki/Str/Wis) when the mount is set and the
    //              absorb-state flag holds. Below: MountAbsorbPrimaryBonus. The magnitude is now resolvable
    //              via TryGetMountBaseRow(...).AbsorbValue -- MountContext.AbsorbValue itself still needs an
    //              assembler wired to that lookup before this stops contributing 0 in practice; the rule is
    //              nonetheless faithfully in place and unchanged by this data recovery.
    //   Tier 3  -- "set-30" exp-scaled bonus: DEFERRED. Its qualifying mount ids are item-table-data-driven
    //              (item sort field == 30, S07_MyGame03.cpp:7467-7475) and unrecoverable from the contract;
    //              its two packed-decimal helpers (ReturnNewPower / ReturnNewAnimalAbsorbBuff) and the
    //              element-defense flat term are only partially pinned; and MountContext carries neither the
    //              packed activity/exp value nor the currently-summoned mount id those helpers need. Not
    //              implemented -- see this workstream's openQuestions.
    //
    // Réf. C++ : Server/Header/Protocol/DEFINE.h:80 (USE_ANIMAL defined unconditionally -- live in the
    // production ReleaseEU33 build, not a single-variant branch), :185-188 (the four grade-tier percentage
    // constants ANIMAL_RATE_ONE_GRADE/TWO_GRADE/THREE_GRADE/FOUR_GRADE = 1.05f/1.10f/1.15f/1.20f -- corrected
    // from an earlier pass's mistaken "_5/10/15/20_GRADE" naming); Server/Header/Protocol/MyAnimal.cpp:16-168
    // (the 94-row mANIMAL_DATA base table, row count independently hand-recounted at 94, correcting an
    // earlier "74" figure -- every row's VALUES are now transcribed in full below, see
    // MountBaseDataByItemId), :17 (column order: hp, mp, dmg, def, hit, dodge, cri, ele_dmg, ele_def, absorb,
    // 3d, abEffect), :176-188 (ANIMALSYSTEM::GetAnimalBaseData, the linear-scan-by-id lookup itself, first
    // match wins, silent fall-through on no match -- modeled by TryGetMountBaseRow), :190-227 (the
    // per-character decode: active-index range check, the activity-gated per-digit power decode in the exact
    // order element-defense/element-attack/dodge/hit/mana/health/defense/damage, and the trailing
    // base-table-row lookup); Server/Header/Protocol/STRUCT.h:1929-1943 (the ANIMAL_BASE struct definition,
    // confirming the column-order comment matches the compiled field order); Server/Header/function.h:2420-
    // 2423 (slot index = active index modulo 10), :2425-2433 (activity/experience = packed counter div/mod
    // 1,000,000); the per-stat MyFactor blocks are cited on each member below.
    // ================================================================================================

    // ---- Tier 0(a): mount base-table row lookup (mANIMAL_DATA) ----

    /// <summary>
    ///     One row of the legacy <c>mANIMAL_DATA</c> table (Server/Header/Protocol/MyAnimal.cpp:16-168),
    ///     keyed by mount/animal item id. Each of the nine stat columns is itself a Tier-1 grade-percentage
    ///     <em>column selector</em> to feed straight into <see cref="ApplyMountGradeMultiplierFourTier" />/
    ///     <see cref="ApplyMountGradeMultiplierThreeTier" /> (0, or one of 5/10/15/20) -- it is NOT a flat
    ///     point amount; that is Tier 2's own, separate, per-digit mechanism
    ///     (<see cref="ComputeMountFlatBonuses" />). Field order matches the column-order comment at the head
    ///     of the source table (MyAnimal.cpp:17) and the compiled <c>ANIMAL_BASE</c> struct
    ///     (STRUCT.h:1929-1943).
    /// </summary>
    /// <param name="AbsorbValue">
    ///     Tier-2b's own input (<see cref="MountAbsorbPrimaryBonus" />) -- added to each of the four primary
    ///     stats while the mount's absorb-state flag holds. Not itself a grade column.
    /// </param>
    /// <param name="ModelId">Cosmetic 3D model id -- rendering only, not a combat stat, not consumed here.</param>
    /// <param name="AbilityEffectId">
    ///     Index into a separate ability/skill-effect table; -1 means "no special ability effect". Not a
    ///     combat stat and not consumed by the stat-contribution pipeline -- out of scope here. Every row
    ///     whose <see cref="AbsorbValue" /> is overridden to 0 against its own family's tier also carries
    ///     -1 here (no row has -1 with a nonzero absorb, and no row with a real ability-effect id has its
    ///     absorb zeroed out beyond what its own tier already calls for).
    /// </param>
    public readonly record struct MountBaseRow(
        int MaxLifeColumn,
        int MaxManaColumn,
        int AttackColumn,
        int DefenseColumn,
        int HitColumn,
        int DodgeColumn,
        int CriticalColumn,
        int ElementAttackColumn,
        int ElementDefenseColumn,
        int AbsorbValue,
        int ModelId,
        int AbilityEffectId);

    /// <summary>
    ///     The full 94-row <c>mANIMAL_DATA</c> catalogue (Server/Header/Protocol/MyAnimal.cpp:18-167), keyed
    ///     by mount/animal item id (the "index" field -- <see cref="MountContext.AnimalNumber" /> once an
    ///     assembler wires it through). Recovered in full by workstream mount-grade-contribution-table --
    ///     previously only the lookup's mechanism and row count were known, and the row count itself was
    ///     mis-reported as 74; the true count, independently hand-recounted from every literal row in the
    ///     source, is 94. An id absent from this table means "no contribution from any of the nine columns,
    ///     absorb, model, or ability-effect" -- exactly the legacy linear-scan lookup's own silent
    ///     fall-through on no match (<see cref="TryGetMountBaseRow" />, MyAnimal.cpp:176-188).
    /// </summary>
    private static readonly FrozenDictionary<int, MountBaseRow> MountBaseDataByItemId =
        new Dictionary<int, MountBaseRow>
        {
            // Tiger, tier 1
            [1301] = new MountBaseRow(0, 5, 5, 0, 0, 0, 0, 5, 0, 30, 0, 24),
            [8301] = new MountBaseRow(0, 5, 5, 0, 0, 0, 0, 5, 0, 30, 0, 24),
            [7001] = new MountBaseRow(0, 5, 5, 0, 0, 0, 0, 5, 0, 30, 0, 24),

            // Pig, tier 1
            [1302] = new MountBaseRow(5, 5, 0, 0, 0, 0, 0, 0, 5, 30, 1, 27),
            [8302] = new MountBaseRow(5, 5, 0, 0, 0, 0, 0, 0, 5, 30, 1, 27),

            // Deer, tier 1 (nonzero critical column -- see the Tier-1 remarks above)
            [1303] = new MountBaseRow(0, 5, 0, 0, 0, 0, 5, 5, 0, 30, 2, 30),
            [8303] = new MountBaseRow(0, 5, 0, 0, 0, 0, 5, 5, 0, 30, 2, 30),

            // Tiger, tier 2
            [1304] = new MountBaseRow(0, 10, 10, 0, 0, 0, 0, 10, 0, 20, 3, 25),
            [8304] = new MountBaseRow(0, 10, 10, 0, 0, 0, 0, 10, 0, 20, 3, 25),
            [559] = new MountBaseRow(0, 10, 10, 0, 0, 0, 0, 10, 0, 20, 3, 25),
            [17044] = new MountBaseRow(0, 10, 10, 0, 0, 0, 0, 10, 0, 20, 3, 25),

            // Pig, tier 2
            [1305] = new MountBaseRow(10, 10, 0, 0, 0, 0, 0, 0, 10, 20, 4, 28),
            [8305] = new MountBaseRow(10, 10, 0, 0, 0, 0, 0, 0, 10, 20, 4, 28),
            [17045] = new MountBaseRow(10, 10, 0, 0, 0, 0, 0, 0, 10, 20, 4, 28),

            // Deer, tier 2 (nonzero critical column)
            [1306] = new MountBaseRow(0, 10, 0, 0, 0, 0, 10, 10, 0, 20, 5, 31),
            [8306] = new MountBaseRow(0, 10, 0, 0, 0, 0, 10, 10, 0, 20, 5, 31),
            [17046] = new MountBaseRow(0, 10, 0, 0, 0, 0, 10, 10, 0, 20, 5, 31),

            // Tiger, tier 3 (685 is the absorb/ability-effect exception -- absorb 0, ability-effect -1)
            [1307] = new MountBaseRow(0, 15, 15, 0, 0, 0, 0, 15, 0, 10, 6, 26),
            [8307] = new MountBaseRow(0, 15, 15, 0, 0, 0, 0, 15, 0, 10, 6, 26),
            [685] = new MountBaseRow(0, 15, 15, 0, 0, 0, 0, 15, 0, 0, 6, -1),
            [814] = new MountBaseRow(0, 15, 15, 0, 0, 0, 0, 15, 0, 10, 6, 26),

            // Pig, tier 3 (683 is the absorb/ability-effect exception)
            [1308] = new MountBaseRow(15, 15, 0, 0, 0, 0, 0, 0, 15, 10, 7, 29),
            [8308] = new MountBaseRow(15, 15, 0, 0, 0, 0, 0, 0, 15, 10, 7, 29),
            [683] = new MountBaseRow(15, 15, 0, 0, 0, 0, 0, 0, 15, 0, 7, -1),
            [819] = new MountBaseRow(15, 15, 0, 0, 0, 0, 0, 0, 15, 10, 7, 29),

            // Deer, tier 3 (nonzero critical column)
            [1309] = new MountBaseRow(0, 15, 0, 0, 0, 0, 15, 15, 0, 10, 8, 32),
            [8309] = new MountBaseRow(0, 15, 0, 0, 0, 0, 15, 15, 0, 10, 8, 32),
            [817] = new MountBaseRow(0, 15, 0, 0, 0, 0, 15, 15, 0, 10, 8, 32),

            // Bear, tier 1 (1451 is the absorb/ability-effect exception)
            [1313] = new MountBaseRow(0, 5, 5, 0, 5, 0, 0, 0, 0, 30, 9, 33),
            [8313] = new MountBaseRow(0, 5, 5, 0, 5, 0, 0, 0, 0, 30, 9, 33),
            [1451] = new MountBaseRow(0, 5, 5, 0, 5, 0, 0, 0, 0, 0, 9, -1),

            // Bear, tier 2
            [1314] = new MountBaseRow(0, 10, 10, 0, 10, 0, 0, 0, 0, 20, 10, 34),
            [8314] = new MountBaseRow(0, 10, 10, 0, 10, 0, 0, 0, 0, 20, 10, 34),
            [17047] = new MountBaseRow(0, 10, 10, 0, 10, 0, 0, 0, 0, 20, 10, 34),

            // Bear, tier 3
            [1315] = new MountBaseRow(0, 15, 15, 0, 15, 0, 0, 0, 0, 10, 11, 35),
            [8315] = new MountBaseRow(0, 15, 15, 0, 15, 0, 0, 0, 0, 10, 11, 35),
            [820] = new MountBaseRow(0, 15, 15, 0, 15, 0, 0, 0, 0, 10, 11, 35),

            // Christmas baseline / event ids (all ability-effect -1; 510/511 also carry a nonzero critical column)
            [1316] = new MountBaseRow(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, -1),
            [8316] = new MountBaseRow(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, -1),
            [510] = new MountBaseRow(0, 0, 10, 0, 10, 0, 15, 0, 0, 0, 12, -1),
            [511] = new MountBaseRow(0, 0, 15, 0, 15, 0, 15, 0, 0, 0, 12, -1),

            // Cat, tier 1
            [1317] = new MountBaseRow(0, 5, 5, 0, 0, 0, 0, 0, 5, 30, 13, 36),
            [8317] = new MountBaseRow(0, 5, 5, 0, 0, 0, 0, 0, 5, 30, 13, 36),

            // Cat, tier 2
            [1318] = new MountBaseRow(0, 10, 10, 0, 0, 0, 0, 0, 10, 20, 14, 37),
            [8318] = new MountBaseRow(0, 10, 10, 0, 0, 0, 0, 0, 10, 20, 14, 37),
            [17048] = new MountBaseRow(0, 10, 10, 0, 0, 0, 0, 0, 10, 20, 14, 37),

            // Cat, tier 3
            [1319] = new MountBaseRow(0, 15, 15, 0, 0, 0, 0, 0, 15, 10, 15, 38),
            [8319] = new MountBaseRow(0, 15, 15, 0, 0, 0, 0, 0, 15, 10, 15, 38),
            [818] = new MountBaseRow(0, 15, 15, 0, 0, 0, 0, 0, 15, 10, 15, 38),

            // Bull, tier 1
            [1320] = new MountBaseRow(5, 5, 0, 0, 0, 0, 0, 5, 0, 30, 16, 42),
            [8320] = new MountBaseRow(5, 5, 0, 0, 0, 0, 0, 5, 0, 30, 16, 42),

            // Bull, tier 2
            [1321] = new MountBaseRow(10, 10, 0, 0, 0, 0, 0, 10, 0, 20, 17, 43),
            [8321] = new MountBaseRow(10, 10, 0, 0, 0, 0, 0, 10, 0, 20, 17, 43),
            [17049] = new MountBaseRow(10, 10, 0, 0, 0, 0, 0, 10, 0, 20, 17, 43),

            // Bull, tier 3 (684 is the absorb/ability-effect exception)
            [1322] = new MountBaseRow(15, 15, 0, 0, 0, 0, 0, 15, 0, 10, 18, 44),
            [8322] = new MountBaseRow(15, 15, 0, 0, 0, 0, 0, 15, 0, 10, 18, 44),
            [684] = new MountBaseRow(15, 15, 0, 0, 0, 0, 0, 15, 0, 0, 18, -1),
            [821] = new MountBaseRow(15, 15, 0, 0, 0, 0, 0, 15, 0, 10, 18, 44),
            [17058] = new MountBaseRow(15, 15, 0, 0, 0, 0, 0, 15, 0, 10, 18, 44),

            // Wolf, tier 1 (nonzero critical column)
            [1323] = new MountBaseRow(0, 5, 0, 0, 0, 5, 5, 0, 0, 30, 19, 39),
            [8323] = new MountBaseRow(0, 5, 0, 0, 0, 5, 5, 0, 0, 30, 19, 39),

            // Wolf, tier 2 (nonzero critical column)
            [1324] = new MountBaseRow(0, 10, 0, 0, 0, 10, 10, 0, 0, 20, 20, 40),
            [8324] = new MountBaseRow(0, 10, 0, 0, 0, 10, 10, 0, 0, 20, 20, 40),
            [17050] = new MountBaseRow(0, 10, 0, 0, 0, 10, 10, 0, 0, 20, 20, 40),

            // Wolf, tier 3 (nonzero critical column)
            [1325] = new MountBaseRow(0, 15, 0, 0, 0, 15, 15, 0, 0, 10, 21, 41),
            [8325] = new MountBaseRow(0, 15, 0, 0, 0, 15, 15, 0, 0, 10, 21, 41),
            [815] = new MountBaseRow(0, 15, 0, 0, 0, 15, 15, 0, 0, 10, 21, 41),

            // Lion, tier 1
            [1326] = new MountBaseRow(0, 5, 0, 5, 0, 5, 0, 0, 0, 30, 22, 45),
            [8326] = new MountBaseRow(0, 5, 0, 5, 0, 5, 0, 0, 0, 30, 22, 45),

            // Lion, tier 2
            [1327] = new MountBaseRow(0, 10, 0, 10, 0, 10, 0, 0, 0, 20, 23, 46),
            [8327] = new MountBaseRow(0, 10, 0, 10, 0, 10, 0, 0, 0, 20, 23, 46),
            [17051] = new MountBaseRow(0, 10, 0, 10, 0, 10, 0, 0, 0, 20, 23, 46),

            // Lion, tier 3
            [1328] = new MountBaseRow(0, 15, 0, 15, 0, 15, 0, 0, 0, 10, 24, 47),
            [8328] = new MountBaseRow(0, 15, 0, 15, 0, 15, 0, 0, 0, 10, 24, 47),
            [816] = new MountBaseRow(0, 15, 0, 15, 0, 15, 0, 0, 0, 10, 24, 47),

            // Puma, tier 1
            [1329] = new MountBaseRow(15, 20, 15, 0, 0, 0, 0, 0, 0, 5, 25, 50),
            [8329] = new MountBaseRow(15, 20, 15, 0, 0, 0, 0, 0, 0, 5, 25, 50),
            [17059] = new MountBaseRow(15, 20, 15, 0, 0, 0, 0, 0, 0, 5, 25, 50),

            // Puma, tier 2
            [1330] = new MountBaseRow(0, 20, 20, 0, 0, 0, 0, 0, 0, 5, 26, 49),
            [8330] = new MountBaseRow(0, 20, 20, 0, 0, 0, 0, 0, 0, 5, 26, 49),
            [17060] = new MountBaseRow(0, 20, 20, 0, 0, 0, 0, 0, 0, 5, 26, 49),

            // Puma, tier 3
            [1331] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 27, 48),
            [8331] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 27, 48),
            [17061] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 27, 48),

            // Puma, tier 3 recolors (stat columns identical to 1331/8331/17061; only the model id varies)
            [1332] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 48, 48),
            [1333] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 49, 48),
            [1334] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 50, 48),
            [1335] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 51, 48),
            [1336] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 52, 48),
            [1337] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 53, 48),
            [1338] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 54, 48),
            [1339] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 55, 48),
            [1340] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 56, 48),
            [1341] = new MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, 57, 48)
        }.ToFrozenDictionary();

    /// <summary>
    ///     Tier 0(a): the base-table row for a mount/animal item id, or <c>false</c> if the id matches none
    ///     of the 94 catalogued rows -- reproducing <c>ANIMALSYSTEM::GetAnimalBaseData</c>'s own linear-scan,
    ///     first-match-wins lookup with silent fall-through on no match (MyAnimal.cpp:176-188). A caller that
    ///     gets <c>false</c> back must leave every stat's contribution at its existing value, not zero it out
    ///     or otherwise treat it as an error -- exactly like any other non-matching id.
    /// </summary>
    public static bool TryGetMountBaseRow(int mountItemId, out MountBaseRow row)
    {
        return MountBaseDataByItemId.TryGetValue(mountItemId, out row);
    }

    // ---- Tier 1: grade percentage multiplier ----

    /// <summary>
    ///     Tier-1 grade-percentage multipliers keyed on a base-table column value. The column value is itself
    ///     both the selector and the percent (a 5 means +5%). Only the exact values 5/10/15/20 are recognized;
    ///     any other nonzero column falls through to no multiply, reproducing MyFactor's discrete case match.
    ///     The four float constants are <c>ANIMAL_RATE_ONE_GRADE</c>/<c>TWO_GRADE</c>/<c>THREE_GRADE</c>/
    ///     <c>FOUR_GRADE</c> = 1.05f/1.10f/1.15f/1.20f (Server/Header/Protocol/DEFINE.h:185-188) -- corrected
    ///     from an earlier pass's mistaken "_5/10/15/20_GRADE" naming; the *values* 5/10/15/20 are the column
    ///     selectors transcribed into <see cref="MountBaseDataByItemId" />, not part of the constant names.
    /// </summary>
    private static readonly FrozenDictionary<int, float> MountGradeMultiplierByColumn =
        new Dictionary<int, float>
        {
            [5] = 1.05f,
            [10] = 1.10f,
            [15] = 1.15f,
            [20] = 1.20f
        }.ToFrozenDictionary();

    /// <summary>
    ///     The stats whose grade switch carries only the 5/10/15 tiers (hit/dodge/critical/element-attack/
    ///     element-defense): a column of 20 is NOT a case for these and falls through to no multiply.
    ///     Confirmed directly against the now-recovered 94-row <see cref="MountBaseDataByItemId" /> table:
    ///     none of these five columns ever holds 20 in any row (only HP/MP/attack/defense reach the top
    ///     tier, on the "Puma"/high-tier rows, MyAnimal.cpp:143-167), so the missing 20-case is a benign
    ///     match to the data, not a bug. Critical's own column is NOT uniformly zero, though: it is nonzero
    ///     (5/10/15, mirroring the mount's own tier) for 18 of the 94 rows -- the whole Deer and Wolf
    ///     families, plus the two Christmas event ids 510/511 -- so this multiplier is genuinely exercised
    ///     for critical, not merely a theoretical case. There is still no separate flat per-digit critical
    ///     additive (Tier 2 has none for critical, see <see cref="MountFlatBonusPerPointByStat" />) -- only
    ///     this Tier-1 percentage multiply ever touches critical.
    /// </summary>
    private static readonly FrozenSet<int> MountGradeThreeTierColumns = new[] { 5, 10, 15 }.ToFrozenSet();

    /// <summary>
    ///     Tier-2 flat per-point multipliers: each decoded grade digit grants this many points of the stat.
    ///     HP +100/digit (MyFactor.cpp:2135-2136), MP +200/digit (:2313-2314), attack +50/digit (:4041-4045),
    ///     defense +100/digit (:4123-4125), hit +100/digit (:4201-4203), dodge +100/digit (:4251-4253),
    ///     element-attack +50/digit (:4318-4320), element-defense +50/digit (:4369-4371). Critical is
    ///     deliberately absent: its grade digit is never decoded and no flat critical additive exists.
    /// </summary>
    private static readonly FrozenDictionary<MountFlatStat, int> MountFlatBonusPerPointByStat =
        new Dictionary<MountFlatStat, int>
        {
            [MountFlatStat.MaxLife] = 100,
            [MountFlatStat.MaxMana] = 200,
            [MountFlatStat.Attack] = 50,
            [MountFlatStat.Defense] = 100,
            [MountFlatStat.Hit] = 100,
            [MountFlatStat.Dodge] = 100,
            [MountFlatStat.ElementAttack] = 50,
            [MountFlatStat.ElementDefense] = 50
        }.ToFrozenDictionary();

    /// <summary>
    ///     Tier-1 multiplier for the four stats with the full 5/10/15/20 tier set -- max HP
    ///     (MyFactor.cpp:1945-1964), max MP (:2259-2278), attack (:2617-2636) and defense (:2951-2970).
    ///     Multiplies <paramref name="runningTotal" /> by the grade percentage for <paramref name="column" />
    ///     with a single float multiply then a truncate-toward-zero cast, matching legacy
    ///     <c>total = (int)(total * ANIMAL_RATE_*_GRADE)</c>. A column of 0 (unset mount, unmatched mount id, or
    ///     a zero column for this stat) or any unrecognized value leaves the total unchanged -- the "mount set
    ///     and this column nonzero" guard is subsumed, exactly as an id of 0 is simply absent from the table.
    /// </summary>
    public static int ApplyMountGradeMultiplierFourTier(int runningTotal, int column)
    {
        return MountGradeMultiplierByColumn.TryGetValue(column, out var multiplier)
            ? (int)(runningTotal * multiplier)
            : runningTotal;
    }

    /// <summary>
    ///     Tier-1 multiplier for the five stats with only the 5/10/15 tiers -- hit/accuracy
    ///     (MyFactor.cpp:3154-3170), dodge/block (:3326-3342), critical (:3416-3432), element-attack
    ///     (:3803-3819) and element-defense (:3964-3980). As <see cref="ApplyMountGradeMultiplierFourTier" />
    ///     but a column of 20 is not recognized and falls through to no multiply.
    /// </summary>
    public static int ApplyMountGradeMultiplierThreeTier(int runningTotal, int column)
    {
        return MountGradeThreeTierColumns.Contains(column)
            ? (int)(runningTotal * MountGradeMultiplierByColumn[column])
            : runningTotal;
    }

    /// <summary>
    ///     Tier-2 flat per-point additive for one stat. Returns 0 unless <paramref name="gradeDigit" /> is
    ///     strictly positive -- the contract's per-digit guard. The mount's own "activity positive"
    ///     precondition is subsumed only if the decoded digit is 0 whenever activity is not positive (which is
    ///     how the decode fills the digits: they are split from the packed power value only when activity &gt;
    ///     0); MountContext does not carry the activity value itself, so if a caller can supply a positive
    ///     digit alongside zero activity it must gate on activity separately -- see openQuestions.
    /// </summary>
    private static int MountFlatPerPoint(MountFlatStat stat, int gradeDigit)
    {
        return gradeDigit > 0 ? MountFlatBonusPerPointByStat[stat] * gradeDigit : 0;
    }

    /// <summary>Tier-2 flat max-HP bonus: +100 per HP grade digit (MyFactor.cpp:2135-2136).</summary>
    public static int MountFlatMaxLifeBonus(int hpGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.MaxLife, hpGradeDigit);
    }

    /// <summary>Tier-2 flat max-MP bonus: +200 per MP grade digit (MyFactor.cpp:2313-2314).</summary>
    public static int MountFlatMaxManaBonus(int mpGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.MaxMana, mpGradeDigit);
    }

    /// <summary>Tier-2 flat attack bonus: +50 per attack grade digit (MyFactor.cpp:4041-4045).</summary>
    public static int MountFlatAttackBonus(int attackGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.Attack, attackGradeDigit);
    }

    /// <summary>Tier-2 flat defense bonus: +100 per defense grade digit (MyFactor.cpp:4123-4125).</summary>
    public static int MountFlatDefenseBonus(int defenseGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.Defense, defenseGradeDigit);
    }

    /// <summary>Tier-2 flat hit/accuracy bonus: +100 per hit grade digit (MyFactor.cpp:4201-4203).</summary>
    public static int MountFlatHitBonus(int hitGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.Hit, hitGradeDigit);
    }

    /// <summary>Tier-2 flat dodge/block bonus: +100 per dodge grade digit (MyFactor.cpp:4251-4253).</summary>
    public static int MountFlatDodgeBonus(int dodgeGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.Dodge, dodgeGradeDigit);
    }

    /// <summary>Tier-2 flat element-attack bonus: +50 per element-attack grade digit (MyFactor.cpp:4318-4320).</summary>
    public static int MountFlatElementAttackBonus(int elementAttackGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.ElementAttack, elementAttackGradeDigit);
    }

    /// <summary>Tier-2 flat element-defense bonus: +50 per element-defense grade digit (MyFactor.cpp:4369-4371).</summary>
    public static int MountFlatElementDefenseBonus(int elementDefenseGradeDigit)
    {
        return MountFlatPerPoint(MountFlatStat.ElementDefense, elementDefenseGradeDigit);
    }

    /// <summary>
    ///     Tier-2's own decode step (MyAnimal.cpp:190-227): the packed animal "power" value's eight decimal
    ///     digits, least-significant first, assigned to named stats in this fixed order -- ones digit =
    ///     element-defense, tens = element-attack, hundreds = dodge, thousands = hit, ten-thousands = mana,
    ///     hundred-thousands = health/max-life, millions = defense, ten-millions = attack. Exactly the first
    ///     eight decimal places are read; a ninth-or-higher-place digit (<paramref name="power" /> at or above
    ///     100,000,000) and the sign of a negative <paramref name="power" /> are never examined here either --
    ///     matching the legacy decode's own unconditional division/modulo, no clamp or range validation
    ///     applied (the contract flags it as an open question whether the write path already guarantees each
    ///     digit stays 0-9 before this decode ever runs; not assumed here, see this workstream's
    ///     openQuestions).
    ///     <para>
    ///         Gated on <paramref name="activity" /> strictly positive, reproducing the legacy decode's own
    ///         guard: when activity is not strictly positive every digit is zero, i.e. the whole per-digit
    ///         decode contributes nothing for that evaluation. Tier 1's grade-percentage multiply is
    ///         unaffected by this gate (driven purely by the static table row, never by activity) and must
    ///         still be applied by the caller separately -- see <see cref="ApplyMountGradeMultiplierFourTier" />/
    ///         <see cref="ApplyMountGradeMultiplierThreeTier" />.
    ///     </para>
    /// </summary>
    public static MountPowerDigits DecodeMountPowerDigits(int power, int activity)
    {
        return activity > 0
            ? new MountPowerDigits(
                power / 100_000 % 10,
                power / 10_000 % 10,
                power / 10_000_000 % 10,
                power / 1_000_000 % 10,
                power / 1_000 % 10,
                power / 100 % 10,
                power / 10 % 10,
                power % 10)
            : default;
    }

    /// <summary>
    ///     Composes <see cref="DecodeMountPowerDigits" /> with the eight Tier-2 flat-bonus methods above into
    ///     one call: the complete set of Tier-2 additive amounts to add to each stat's own running total, from
    ///     the two raw legacy inputs (the packed power value and the active slot's activity). Tier-2 is a
    ///     per-stat flat add, never a shared pool, so this is a pure convenience composition over the existing
    ///     primitives above, not a new formula -- the equivalent single-call entry point once a future
    ///     assembler threads a real (power, activity) pair through (see this workstream's openQuestions for
    ///     why that channel does not exist on <see cref="MountContext" /> yet).
    /// </summary>
    public static MountFlatBonuses ComputeMountFlatBonuses(int power, int activity)
    {
        var digits = DecodeMountPowerDigits(power, activity);
        return new MountFlatBonuses(
            MountFlatMaxLifeBonus(digits.MaxLife),
            MountFlatMaxManaBonus(digits.MaxMana),
            MountFlatAttackBonus(digits.Attack),
            MountFlatDefenseBonus(digits.Defense),
            MountFlatHitBonus(digits.Hit),
            MountFlatDodgeBonus(digits.Dodge),
            MountFlatElementAttackBonus(digits.ElementAttack),
            MountFlatElementDefenseBonus(digits.ElementDefense));
    }

    // ---- Tier 2b: absorb -> primary stats ----

    /// <summary>
    ///     Tier-2b: the matched base-table row's absorb column, added to EACH of the four primary stats
    ///     (Vitality, Ki, Strength, Wisdom) while the mount is set and the mount absorb-state flag holds
    ///     (MyFactor.cpp:1705-1708 for Vitality; :1765-1766 / :1824-1825 / :1883-1884 for Ki/Strength/Wisdom).
    ///     Consumed in the BASE layer -- added to each primary attribute inside ComputeBaseStats, so it
    ///     propagates into every derived stat. The magnitude is <see cref="MountContext.AbsorbValue" />, now
    ///     resolvable in full via <see cref="TryGetMountBaseRow" />'s recovered <c>AbsorbValue</c> column (30
    ///     for tier-1 mounts down to 5 for Puma, with four family-specific 0-overrides -- see
    ///     <see cref="MountBaseDataByItemId" />). No assembler wires that lookup into
    ///     <see cref="MountContext" /> yet, so this still contributes 0 in practice today; the rule itself is
    ///     nonetheless faithfully in place and unaffected by that still-pending wiring step.
    ///     <para>
    ///         Gate note: <see cref="MountContext.AbsorbActive" /> is the assembler-cooked
    ///         <c>aAnimalAbsorbState != 0</c>, while the legacy line checks <c>== 1</c> (MyFactor.cpp:1706) --
    ///         equivalent whenever the stored flag only ever holds 0 or 1, which is its documented domain.
    ///     </para>
    /// </summary>
    public static int MountAbsorbPrimaryBonus(MountContext mount)
    {
        return mount.AnimalNumber != 0 && mount.AbsorbActive ? mount.AbsorbValue : 0;
    }

    // ---- Tier 2: flat per-point additive ----

    private enum MountFlatStat
    {
        MaxLife,
        MaxMana,
        Attack,
        Defense,
        Hit,
        Dodge,
        ElementAttack,
        ElementDefense
    }

    // ---- Tier 2's own input: the per-digit power decode (workstream B8-myanimal-table) ----
    //
    // Closes the exact gap MountFlatPerPoint's own doc above flagged: "if a caller can supply a positive
    // digit alongside zero activity it must gate on activity separately". The B8-myanimal-table contract
    // (Server/Header/Protocol/MyAnimal.cpp:190-227) makes the activity gate explicit at the DECODE step
    // itself, not just at each flat-bonus call site, and pins the previously-ambiguous digit-place -> stat
    // mapping (this also independently confirms Domain/Mounts/MountPowerCodec's own wire-attribute-index
    // ordering is legacy-consistent: attribute-index 1 = MountPowerCodec place 7 = ten-millions = attack,
    // ... index 8 = place 0 = ones = element-defense -- the exact reverse-LSD order this contract states).
    //
    // This decode is the per-digit MECHANISM only -- the separate 94-row MyAnimal base table itself (Tier-1's
    // grade columns and Tier-2b's AbsorbValue) is now fully transcribed above as MountBaseRow/
    // MountBaseDataByItemId/TryGetMountBaseRow (workstream mount-grade-contribution-table), closing the gap
    // the prior B8-mount pass had left open. The per-digit power value itself is likewise not yet threaded
    // from PlayerRuntimeState/MountContext into a call site (no Power/Activity channel exists on MountContext
    // today) -- still an openQuestions item, not guessed here.

    /// <summary>
    ///     The eight per-stat point-investment digits <see cref="DecodeMountPowerDigits" /> decodes from the
    ///     packed animal "power" value, named to match <see cref="MountFlatStat" />. Critical has no member
    ///     here: the legacy per-digit attribute structure declares a ninth slot for it, but the decode never
    ///     assigns it and no stat computation reads it either (MyFactor.cpp:3416-3432's own full-body search
    ///     finds no reference) -- it must not be modeled as a live channel.
    /// </summary>
    public readonly record struct MountPowerDigits(
        int MaxLife = 0,
        int MaxMana = 0,
        int Attack = 0,
        int Defense = 0,
        int Hit = 0,
        int Dodge = 0,
        int ElementAttack = 0,
        int ElementDefense = 0);

    /// <summary>The eight Tier-2 flat additive amounts <see cref="ComputeMountFlatBonuses" /> produces.</summary>
    public readonly record struct MountFlatBonuses(
        int MaxLife = 0,
        int MaxMana = 0,
        int Attack = 0,
        int Defense = 0,
        int Hit = 0,
        int Dodge = 0,
        int ElementAttack = 0,
        int ElementDefense = 0);
}
