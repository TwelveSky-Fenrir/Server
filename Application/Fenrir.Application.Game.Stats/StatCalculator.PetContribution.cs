using System.Collections.Frozen;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ==================================================================================================
    // WORKSTREAM B8 (pet stat contributions) -- the AMULET-pet graded/flat bonuses, the stepped attack
    // bonus, and the grow-percent, ported from PETSYSTEM (GameSystem_07_Pet.cpp). These are PURE
    // computations: an unusable input (unknown index, wrong item sort, an excluded amulet index, a
    // packed-byte tens digit that does not match the requested type, an inactive/unrecognised pet) always
    // resolves to a zero contribution -- a silent no-op, never an error or disconnect (contract
    // Error/failure semantics).
    //
    // ALREADY BUILT ELSEWHERE -- deliberately NOT duplicated here:
    //   * The growth-pet flat Life/Mana/AttackPower/DefensePower values and their "pet double" gate:
    //     Domain PetGrowthCalculator + StatCalculator.ApplyPetDoubleRule (already wired into ComputeMaxLife/
    //     ComputeMaxMana/ComputeEffectiveStats). GameSystem_07_Pet.cpp:532-694,1578-1720.
    //   * The grow STEP (ReturnGrowStep) tier-crossing test: Domain PetGrowthTierCalculator.
    //   * The eight tier maxima: Domain PetGrowthCaps.
    //   * The amulet LIFE/MANA flat tables (sort 28, ReturnAmuletLifeValue/ReturnAmuletManaValue):
    //     Domain PetSlotAmuletBonusTable -- built there but deliberately UNWIRED pending the "Custom
    //     Phoenix Amulet" double-count contradiction (see that class's remarks). This file adds the
    //     ATTACK/DEFENSE siblings of those tables, which inherit the SAME wiring blocker (see
    //     PetAmuletAttackBonus/PetAmuletDefenseBonus remarks) -- so they are built, not yet wired.
    //
    // MISSING RUNTIME INPUT (blocks wiring the graded IS/IU/IM/growth-value contributions):
    //   Fenrir models the pet equip slot's sub-field 2 only as PlayerRuntimeState.PetGrowth, an int GROWTH
    //   COUNTER (for a sort-22 growth pet). The legacy reads that same sub-field, for a sort-28/31/32/33
    //   AMULET pet, as a packed 4-byte GRADED encoding (function.h:317-343). No Fenrir field carries that
    //   packed graded value, so the graded methods below have no live input yet. The packed value MAY be
    //   the pet slot's four ItemValueCodec upgrade bytes (EquippedItemSlot.Enchant/Combine/Refine/Socket)
    //   -- but that mapping is UNCONFIRMED and must not be assumed (see openQuestions). The graded methods
    //   therefore take a caller-supplied packed int and self-decode; they are exercised directly by tests
    //   until the input field lands.
    //
    // Methods are PUBLIC static (B6 StellarCore / B5 rune precedent): the getter/skill-pipeline call sites
    // are landed by a separate serial integration pass (see wiringManifest), so private methods would trip
    // IDE0051 unused-private-member under TreatWarningsAsErrors, and tests exercise them directly before
    // wiring lands.
    // ==================================================================================================

    /// <summary>FITEM_SORT::INEW_PET -- the only item sort the amulet (sort-28) contributions accept.</summary>
    private const int PetAmuletSort = 28;

    /// <summary>
    ///     Indices explicitly rejected by the graded IS/IU/IM functions (GameSystem_07_Pet.cpp:1080-1083,
    ///     :1341-1344, :1369-1372). They grant NO graded bonus even though some still appear in the flat
    ///     amulet tables -- the flat tables do NOT apply this rejection (contract edge case).
    /// </summary>
    private static readonly FrozenSet<int> PetGradedAmuletExcludedIds =
        new[] { 2253, 2254, 2261, 2262, 2300, 2301 }.ToFrozenSet();

    // ---- Graded IS bonus (GameSystem_07_Pet.cpp:1064-1325) ----
    // Six per-type grade ladders, each indexed by the grade (units digit, 0-9). Value = step*(grade+1):
    // grade 0 yields the ladder's first (non-zero) entry, grade 9 its last -- confirmed by the IM type-5
    // ladder below, whose cited grade-0 entry (0.3) is a real bonus, not zero.

    private static readonly int[] PetIsLadderType1 = [30, 60, 90, 120, 150, 180, 210, 240, 270, 300];
    private static readonly int[] PetIsLadderType2 = [200, 400, 600, 800, 1000, 1200, 1400, 1600, 1800, 2000];
    private static readonly int[] PetIsLadderType3 = [20, 40, 60, 80, 100, 120, 140, 160, 180, 200];
    private static readonly int[] PetIsLadderType4 = [250, 500, 750, 1000, 1250, 1500, 1750, 2000, 2250, 2500];
    private static readonly int[] PetIsLadderType5 = [100, 200, 300, 400, 500, 600, 700, 800, 900, 1000];
    private static readonly int[] PetIsLadderType6 = [100, 200, 300, 400, 500, 600, 700, 800, 900, 1000];

    // ---- Graded IM bonus (GameSystem_07_Pet.cpp:1353-1576) ----
    // Five per-type ladders (no type 6). Fractional -- returned as float; the consuming getter truncates
    // per its own accumulator (see wiringManifest note on the type-5/critical precision concern).

    private static readonly float[] PetImLadderType1 = [30f, 60f, 90f, 120f, 150f, 180f, 210f, 240f, 270f, 300f];

    private static readonly float[] PetImLadderType2 =
        [200f, 400f, 600f, 800f, 1000f, 1200f, 1400f, 1600f, 1800f, 2000f];

    private static readonly float[] PetImLadderType3 =
        [100f, 200f, 300f, 400f, 500f, 600f, 700f, 800f, 900f, 1000f];

    private static readonly float[] PetImLadderType4 =
        [100f, 200f, 300f, 400f, 500f, 600f, 700f, 800f, 900f, 1000f];

    /// <summary>
    ///     Type-5 (critical) IM ladder. LEGACY ANOMALY REPLICATED FOR EXACT PARITY: grade 0 AND grade 1 both
    ///     return 0.3 (grade 1 is almost certainly intended to be 0.6, GameSystem_07_Pet.cpp:1536-1541); the
    ///     rest of the sequence climbs by 0.3 per grade. This is a deliberate legacy-parity choice, NOT an
    ///     oversight -- if a product decision later corrects grade 1 to 0.6, change it HERE and record it
    ///     (contract open question "IM type-5 grade anomaly").
    /// </summary>
    private static readonly float[] PetImLadderType5 = [0.3f, 0.3f, 0.9f, 1.2f, 1.5f, 1.8f, 2.1f, 2.4f, 2.7f, 3.0f];

    // ---- Amulet flat ATTACK / DEFENSE tables (GameSystem_07_Pet.cpp:856-910, sort 28) ----
    // Only the concrete magnitudes the contract transcribes (8290 and the 76000-series) are populated; every
    // other qualifying id resolves to 0 = "not yet transcribed", NOT proof the legacy value is zero (same
    // posture Domain PetSlotAmuletBonusTable takes for its life/mana half). The flat tables gate on sort 28
    // only and do NOT apply the graded excluded-index rejection.

    private static readonly FrozenDictionary<int, int> PetAmuletAttackTable = new Dictionary<int, int>
    {
        [8290] = 275,
        [76000] = 3000,
        [76001] = 3000,
        [76002] = 3000,
        [76003] = 3000,
        [76004] = 3000,
        [76005] = 3000,
        [76006] = 4000,
        [76007] = 5000
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, int> PetAmuletDefenseTable = new Dictionary<int, int>
    {
        [8290] = 550,
        [76000] = 3000,
        [76001] = 3000,
        [76002] = 3000,
        [76003] = 3000,
        [76004] = 3000,
        [76005] = 5000,
        [76006] = 7500,
        [76007] = 12500
    }.ToFrozenDictionary();

    // ---- packed-value signed-byte decoders (Server/Header/function.h:317-343) ----
    // The sub-field-2 int reinterpreted as four independent SIGNED bytes: byte 0 = IS, byte 1 = IU,
    // byte 2 = IM, byte 3 = IZ. Under the "tens digit = stat type (1-6), units digit = grade (0-9)"
    // convention the encoded values stay small and positive, so signedness never changes a valid result --
    // but the decode is preserved as signed to match the legacy intent exactly.

    /// <summary>Byte 0 ("IS") of the packed pet value, signed.</summary>
    public static int DecodePetIsByte(int packedValue)
    {
        return unchecked((sbyte)packedValue);
    }

    /// <summary>Byte 1 ("IU") of the packed pet value, signed.</summary>
    public static int DecodePetIuByte(int packedValue)
    {
        return unchecked((sbyte)(packedValue >> 8));
    }

    /// <summary>Byte 2 ("IM") of the packed pet value, signed.</summary>
    public static int DecodePetImByte(int packedValue)
    {
        return unchecked((sbyte)(packedValue >> 16));
    }

    /// <summary>Byte 3 ("IZ") of the packed pet value, signed.</summary>
    public static int DecodePetIzByte(int packedValue)
    {
        return unchecked((sbyte)(packedValue >> 24));
    }

    private static bool IsGradedAmuletEligible(int petItemId, int petSort)
    {
        return petSort == PetAmuletSort && !PetGradedAmuletExcludedIds.Contains(petItemId);
    }

    /// <summary>
    ///     Graded IS bonus for <paramref name="statType" /> (1=attack, 2=max life, 3=attack success/block,
    ///     4=max mana, 5=element attack, 6=element defense). Requires sort 28, a non-excluded index, and the
    ///     IS byte's tens digit to equal <paramref name="statType" />; the grade is the units digit.
    /// </summary>
    public static int PetGradedIsBonus(int petItemId, int petSort, int packedValue, int statType)
    {
        if (!IsGradedAmuletEligible(petItemId, petSort))
            return 0;

        var isByte = DecodePetIsByte(packedValue);
        if (isByte / 10 != statType)
            return 0;

        var grade = isByte % 10;
        if (grade is < 0 or > 9)
            return 0;

        var ladder = statType switch
        {
            1 => PetIsLadderType1,
            2 => PetIsLadderType2,
            3 => PetIsLadderType3,
            4 => PetIsLadderType4,
            5 => PetIsLadderType5,
            6 => PetIsLadderType6,
            _ => null
        };

        return ladder is null ? 0 : ladder[grade];
    }

    // ---- Graded IU bonus (GameSystem_07_Pet.cpp:1327-1351) ----

    /// <summary>
    ///     Graded IU bonus: when the IU byte's tens digit equals <paramref name="statType" />, returns the
    ///     raw grade digit (0-9); otherwise 0. Consumed per bonus-skill channel (see
    ///     <see cref="PetBonusSkillStatType" />).
    /// </summary>
    public static int PetGradedIuBonus(int petItemId, int petSort, int packedValue, int statType)
    {
        if (!IsGradedAmuletEligible(petItemId, petSort))
            return 0;

        var iuByte = DecodePetIuByte(packedValue);
        if (iuByte / 10 != statType)
            return 0;

        var grade = iuByte % 10;
        return grade is < 0 or > 9 ? 0 : grade;
    }

    /// <summary>
    ///     Graded IM bonus for <paramref name="statType" /> (1=attack, 2=max life, 3=element attack,
    ///     4=element defense, 5=critical). Same sort-28 + excluded-index gate and tens-digit-equals-type
    ///     rule as <see cref="PetGradedIsBonus" />; the grade is the IM byte's units digit.
    /// </summary>
    public static float PetGradedImBonus(int petItemId, int petSort, int packedValue, int statType)
    {
        if (!IsGradedAmuletEligible(petItemId, petSort))
            return 0f;

        var imByte = DecodePetImByte(packedValue);
        if (imByte / 10 != statType)
            return 0f;

        var grade = imByte % 10;
        if (grade is < 0 or > 9)
            return 0f;

        var ladder = statType switch
        {
            1 => PetImLadderType1,
            2 => PetImLadderType2,
            3 => PetImLadderType3,
            4 => PetImLadderType4,
            5 => PetImLadderType5,
            _ => null
        };

        return ladder is null ? 0f : ladder[grade];
    }

    /// <summary>
    ///     Amulet flat attack bonus (event-gated at the call site, MyFactor.cpp:4069-4071). WIRING BLOCKED:
    ///     the 76005/76006/76007 rows overlap the existing "Custom Phoenix Amulet" attack terms
    ///     (<see cref="PhoenixFlatBonus" /> in ComputeAttackPower), so wiring this alongside them would
    ///     double-count -- resolve the same contradiction Domain PetSlotAmuletBonusTable flags first.
    /// </summary>
    public static int PetAmuletAttackBonus(int petItemId, int petSort)
    {
        return petSort == PetAmuletSort && PetAmuletAttackTable.TryGetValue(petItemId, out var value) ? value : 0;
    }

    /// <summary>
    ///     Amulet flat defense bonus (event-gated at the call site, MyFactor.cpp:4140-4144). WIRING BLOCKED:
    ///     the 76005/76006/76007 rows (5000/7500/12500) exactly equal the existing
    ///     <see cref="PhoenixFlatBonus" /> defense term (5000/7500/12500) in ComputeDefensePower, so this is
    ///     very likely the SAME contribution differently attributed -- do not wire until that is resolved.
    /// </summary>
    public static int PetAmuletDefenseBonus(int petItemId, int petSort)
    {
        return petSort == PetAmuletSort && PetAmuletDefenseTable.TryGetValue(petItemId, out var value) ? value : 0;
    }

    // ---- Grow-percent + stepped attack bonus (GameSystem_07_Pet.cpp:437-530, :1722-1802) ----
    // Both consume the growth-pet TIER MAXIMUM (40M/80M/160M/320M -- Domain PetGrowthCaps indices 0-3),
    // resolved by the caller from the pet item id's growth family. tierMax <= 0 signals an unrecognised
    // index and yields 0 (matching the legacy default: return 0).

    /// <summary>
    ///     The legacy integer-then-fractional growth percentage (GameSystem_07_Pet.cpp:529, :1790): the tier
    ///     maximum is first integer-divided by 100000, the grow value is integer-divided by that reduced
    ///     figure, and the quotient is divided by 1000 as a fraction. Net: 100 when grow equals the tier max,
    ///     200 when grow equals twice the tier max. No grow-below-1 guard here -- that guard belongs to
    ///     <see cref="PetGrowPercent" /> only.
    /// </summary>
    private static float PetGrowthPercentRaw(int growValue, int tierMax)
    {
        if (tierMax <= 0)
            return 0f;

        var reducedMax = tierMax / 100000;
        if (reducedMax <= 0)
            return 0f;

        return growValue / reducedMax / 1000f;
    }

    /// <summary>
    ///     <c>PETSYSTEM::ReturnGrowPercent</c>: the pet's growth percentage. Below-1 grow (or an unrecognised
    ///     index, signalled by <paramref name="tierMax" /> &lt;= 0) yields 0. Reaching exactly 200 -- grow at
    ///     twice the tier max -- is the recognised "fully evolved" ceiling (NOT 100). No EffectiveStats getter
    ///     consumes this; it exists for the out-of-slice pet fusion/merge gate (contract Outputs).
    /// </summary>
    public static float PetGrowPercent(int growValue, int tierMax)
    {
        if (growValue < 1)
            return 0f;

        return PetGrowthPercentRaw(growValue, tierMax);
    }

    /// <summary>
    ///     <c>PETSYSTEM::ReturnAttackPower2</c>: an integer attack addend that begins only once the pet
    ///     exceeds 101% growth and steps in fifty-point increments to 250 at 200%+ growth. Zero for an
    ///     inactive pet (<paramref name="activity" /> &lt; 1) or an unrecognised index
    ///     (<paramref name="tierMax" /> &lt;= 0); there is NO grow-below-1 gate (a &lt;101% growth simply
    ///     lands in the first step, which is 0).
    /// </summary>
    public static int PetSteppedAttackBonus(int growValue, int tierMax, int activity)
    {
        if (activity < 1 || tierMax <= 0)
            return 0;

        var percent = PetGrowthPercentRaw(growValue, tierMax);
        return percent switch
        {
            < 101f => 0,
            < 125f => 50,
            < 150f => 100,
            < 175f => 150,
            < 200f => 200,
            _ => 250
        };
    }

    // ---- Graded growth value, live decode paths (GameSystem_07_Pet.cpp:976-1062) ----
    // Only the two branches with a live consumer in the shipped build are ported: type 1/sub-sort 1 (attack)
    // and type 2/sub-sort 2 (bonus skill). The type-3 (IM 21/22/23) and type-4 (IZ 31/32/33) decode paths
    // have no live caller in any built configuration (contract open question "two unreachable branches") and
    // are deliberately omitted. The utility-sort gate (item sorts 31/32/33 -> recognised utility-sorts, the
    // :981-983 precondition) is OUT OF SLICE -- these decoders assume the caller already confirmed the pet is
    // a graded-growth pet (see openQuestions).

    /// <summary>
    ///     Type-1 / sub-sort-1 graded growth value: reads the IS byte, mapping 1/2/3 to 1/2/3 (else 0). The
    ///     consuming attack getter applies this as a PERCENTAGE of current attack, not a flat addend
    ///     (MyFactor.cpp:4066) -- the exact percentage-application arithmetic is a getter-wiring detail (see
    ///     wiringManifest), not modelled here.
    /// </summary>
    public static int PetGrowthValueAttackGrade(int packedValue)
    {
        return DecodePetIsByte(packedValue) switch { 1 => 1, 2 => 2, 3 => 3, _ => 0 };
    }

    /// <summary>
    ///     Type-2 / sub-sort-2 graded growth value: reads the IU byte, mapping codes 11/12/13 to 1/2/3
    ///     (else 0). Added flat to the bonus-skill value (MyFactor.cpp:4572).
    /// </summary>
    public static int PetGrowthValueBonusSkillGrade(int packedValue)
    {
        return DecodePetIuByte(packedValue) switch { 11 => 1, 12 => 2, 13 => 3, _ => 0 };
    }

    /// <summary>
    ///     Maps a bonus-skill channel's skill index to the graded-IU stat type it draws
    ///     (MyFactor.cpp:4550-4572): 103-&gt;1, 82-&gt;2, 83-&gt;3, 105-&gt;4, 104-&gt;5, 84-&gt;6; any other
    ///     skill index -&gt; 0 (no IU bonus). For the future bonus-skill pipeline that folds
    ///     <see cref="PetGradedIuBonus" /> in per channel (no EffectiveStats getter owns it today).
    /// </summary>
    public static int PetBonusSkillStatType(int skillIndex)
    {
        return skillIndex switch
        {
            103 => 1,
            82 => 2,
            83 => 3,
            105 => 4,
            104 => 5,
            84 => 6,
            _ => 0
        };
    }
}
