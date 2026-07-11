using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ======================================================================================================
    // WORKSTREAM B7 -- drunk-potion stat wrappers (ids 878-882) + rage-buff attack multiplier (dormant)
    // ======================================================================================================
    //
    // Réf. legacy: MyFactor's per-read combat-stat getters fold in a "drunk" ("Druk") modifier when a drunk
    // effect is live, and the cached base-attack getter folds in a rage-buff multiplier when the rage-buff
    // STATE flag equals 1. This file owns the id/value tables and the pure contribution functions; the actual
    // call-site insertions into the getters live in the integration pass (see this workstream's wiringManifest).
    //
    // GATE MODEL. Legacy's drunk gate (MyUtil::GetDrukValue, S07_MyGame03.cpp:11394-11397) is
    // "drunk-timer > 0 AND the queried id equals the id in the single currently-selected bottle slot". That
    // gate is resolved UPSTREAM and delivered here as ZoneContext.DrunkStateId: the assembler is responsible
    // for writing 0 when the timer is not positive, and the live drunk id otherwise. So every wrapper below
    // treats DrunkStateId as the already-gated live id and simply checks membership in the recognized set --
    // an unrecognized id (including 0 "not drunk") is a no-op. (Assembler wiring / the missing runtime source
    // field are called out in this workstream's openQuestions.)
    //
    // CACHED vs EFFECTIVE (must be preserved -- contract edge case). Two legs are CACHED: they belong in the
    // SetBasicAbilityFromEquip base cache (Fenrir: ComputeBaseStats) and are recomputed only on an equip/level/
    // drunk-activation/expiry event -- the 878 max-life -10% (GetBaseMaxLife, MyFactor.cpp:2184-2185) and the
    // 880 critical-defence -10% (GetBaseCriticalDefence, MyFactor.cpp:3588-3589). Every other leg is EFFECTIVE:
    // re-evaluated on each combat stat read (Fenrir: ComputeEffectiveStats). Treating all legs uniformly (all
    // cached, or all effective) would diverge from legacy timing.
    //
    // ARITHMETIC. The contract states all drunk/rage percentage math is "the truncated-toward-zero integer of
    // the value multiplied by the factor". Every stat value here is non-negative, so truncation toward zero ==
    // floor. This file uses EXACT integer-percentage arithmetic -- value * percent / 100 with integer division
    // (see ScaleByPercent) -- so, e.g., a -10% of 1000 is exactly 900, not the 899 a single-precision float
    // constant would truncate to. Whether legacy performed these specific multiplies in single-precision float
    // (which could differ by +/-1 at a boundary) rather than at the clean stated magnitude is not recoverable
    // from this contract; recorded in openQuestions. The clean magnitude is the faithful reading of the
    // contract as written.

    /// <summary>
    ///     The five drunk-potion ids the legacy combat-stat wrappers recognise (878-882). Any other id --
    ///     including 0 ("not drunk") -- produces no drunk modifier. Réf. contract Inputs / Side effects.
    /// </summary>
    public static readonly FrozenSet<int> DrunkPotionIds = new[] { 878, 879, 880, 881, 882 }.ToFrozenSet();

    /// <summary>
    ///     The per-stat drunk multipliers for each recognised drunk id, expressed as whole-number percentages
    ///     (100 = unchanged), applied via <see cref="ScaleByPercent" />. A stat left at 100 for a given id is
    ///     not touched by that id. Because exactly one drunk id is ever live at a time (a single selected slot),
    ///     the 878 and 879 attack-power legs can never both fire on one read. Réf. legacy per-id legs cited
    ///     inline on each entry (all in <c>Server/Header/Protocol/MyFactor.cpp</c>).
    /// </summary>
    public static readonly FrozenDictionary<int, DrunkEffect> DrunkEffectsById = new Dictionary<int, DrunkEffect>
    {
        // 878: max-life -10% (CACHED, :2184-2185); attack power +10% (EFFECTIVE, :4050-4051).
        [878] = new(90, 110),
        // 879: attack power -20% (EFFECTIVE, :4052-4053); defense power doubled (EFFECTIVE, :4128-4129).
        [879] = new(AttackPowerPercent: 80, DefensePowerPercent: 200),
        // 880: critical-defence -10% (CACHED, :3588-3589); critical +5% (EFFECTIVE, :4456-4457).
        [880] = new(CriticalPercent: 105, CriticalDefencePercent: 90),
        // 881: attack success (hit) -20% (EFFECTIVE, :4207-4208). One-sided: no offsetting benefit was located
        // within MyFactor.cpp -- see this workstream's openQuestions (do not invent one).
        [881] = new(AttackSuccessPercent: 80),
        // 882: element-attack +30% (EFFECTIVE, :4325-4326); element-defense -50% (EFFECTIVE, :4377-4378).
        [882] = new(ElementAttackPercent: 130, ElementDefensePercent: 50)
    }.ToFrozenDictionary();

    /// <summary>
    ///     The rage-buff gauge-to-multiplier table (MyUtil::ReturnRageBuffRate,
    ///     <c>Server/ts25zone/S07_MyGame03.cpp:9893-9934</c>), expressed as whole-number percentages
    ///     (gauge 1-10 -> 105..140). Every gauge value not present here -- including 0 and anything above 10 --
    ///     resolves to 100 (no change) via <see cref="ResolveRageBuffPercent" />. This table is fully
    ///     recoverable and modelled, but the multiplier that consumes it is DORMANT in the shipped build (see
    ///     <see cref="ApplyRageAttackMultiplier" />).
    /// </summary>
    public static readonly FrozenDictionary<int, int> RageBuffPercentByGauge = new Dictionary<int, int>
    {
        [1] = 105, // 1.05
        [2] = 107, // 1.07
        [3] = 109, // 1.09
        [4] = 111, // 1.11
        [5] = 114, // 1.14
        [6] = 117, // 1.17
        [7] = 120, // 1.20
        [8] = 125, // 1.25
        [9] = 130, // 1.30
        [10] = 140 // 1.40
    }.ToFrozenDictionary();

    // No ZoneContext rage-buff-STATE field exists (legacy aRageBuffState, Server/Header/Protocol/STRUCT.h:507),
    // and the shipped build never sets it to 1 -- so the rage multiplier is never active. Modelled as a property
    // (not a const) so the exact multiply above stays reachable code for the future wiring pass. See openQuestions.
    private static bool RageBuffStateActive => false;

    /// <summary>
    ///     Scales <paramref name="value" /> by a whole-number percentage using exact integer arithmetic,
    ///     truncated toward zero (100 = unchanged). The widening to <c>long</c> guards the intermediate product
    ///     against overflow for large stat values; every stat this is applied to is non-negative, so the
    ///     integer division truncates toward zero exactly as the contract requires.
    /// </summary>
    public static int ScaleByPercent(int value, int percent)
    {
        return percent == 100 ? value : (int)((long)value * percent / 100);
    }

    /// <summary>
    ///     Resolves the live drunk effect for a gated drunk-state id (as delivered on
    ///     <see cref="ZoneContext.DrunkStateId" />), or <c>null</c> when the id is not one of the five
    ///     recognised drunk potions (0 "not drunk" included).
    /// </summary>
    public static DrunkEffect? ResolveDrunkEffect(int drunkStateId)
    {
        return DrunkEffectsById.TryGetValue(drunkStateId, out var effect) ? effect : null;
    }

    /// <summary>
    ///     Resolves the rage-buff multiplier percentage for a gauge value (100 = no change for any gauge outside
    ///     1-10, including 0). Réf. <c>S07_MyGame03.cpp:9893-9934</c>.
    /// </summary>
    public static int ResolveRageBuffPercent(int gauge)
    {
        return RageBuffPercentByGauge.TryGetValue(gauge, out var percent) ? percent : 100;
    }

    // ---- CACHED drunk legs (feed ComputeBaseStats) ----

    /// <summary>
    ///     878 max-life -10%, CACHED leg (GetBaseMaxLife, MyFactor.cpp:2184-2185). Applied to the fully summed
    ///     base max life (i.e. after every additive contribution) inside <c>ComputeMaxLife</c>.
    /// </summary>
    public static int ApplyDrunkMaxLife(int maxLife, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(maxLife, e.MaxLifePercent)
            : maxLife;
    }

    /// <summary>
    ///     880 critical-defence -10%, CACHED leg (GetBaseCriticalDefence, MyFactor.cpp:3588-3589). Applied to the
    ///     fully summed base critical-defence inside <c>ComputeCriticalDefence</c>.
    /// </summary>
    public static int ApplyDrunkCriticalDefence(int criticalDefence, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(criticalDefence, e.CriticalDefencePercent)
            : criticalDefence;
    }

    // ---- EFFECTIVE drunk legs (feed ComputeEffectiveStats) ----

    /// <summary>
    ///     878 attack power +10% / 879 attack power -20%, EFFECTIVE leg (MyFactor.cpp:4050-4053). Operates on the
    ///     base attack value. Legacy order: after the (unmodelled) mix-skill 5% bonus, before the (unmodelled)
    ///     tribe-role flat bonus. Only one of 878/879 can ever be the live id, so at most one applies.
    /// </summary>
    public static int ApplyDrunkAttackPower(int attackPower, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(attackPower, e.AttackPowerPercent)
            : attackPower;
    }

    /// <summary>
    ///     879 defense power doubled, EFFECTIVE leg (MyFactor.cpp:4128-4129).
    /// </summary>
    public static int ApplyDrunkDefensePower(int defensePower, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(defensePower, e.DefensePowerPercent)
            : defensePower;
    }

    /// <summary>
    ///     880 critical +5%, EFFECTIVE leg (MyFactor.cpp:4456-4457).
    /// </summary>
    public static int ApplyDrunkCritical(int critical, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(critical, e.CriticalPercent)
            : critical;
    }

    /// <summary>
    ///     881 attack success (hit) -20%, EFFECTIVE leg (MyFactor.cpp:4207-4208). This is the only leg id 881
    ///     contributes inside the combat-stat code -- it has no offsetting benefit here (see openQuestions).
    /// </summary>
    public static int ApplyDrunkAttackSuccess(int attackSuccess, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(attackSuccess, e.AttackSuccessPercent)
            : attackSuccess;
    }

    /// <summary>
    ///     882 element-attack +30%, EFFECTIVE leg (MyFactor.cpp:4325-4326).
    /// </summary>
    public static int ApplyDrunkElementAttack(int elementAttack, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(elementAttack, e.ElementAttackPercent)
            : elementAttack;
    }

    /// <summary>
    ///     882 element-defense -50%, EFFECTIVE leg (MyFactor.cpp:4377-4378).
    /// </summary>
    public static int ApplyDrunkElementDefense(int elementDefense, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(elementDefense, e.ElementDefensePercent)
            : elementDefense;
    }

    // ---- Rage-buff attack multiplier (DORMANT) ----

    /// <summary>
    ///     Rage-buff multiplier of the CACHED base attack power (MyFactor.cpp:2614-2615): when the rage-buff
    ///     STATE flag equals 1, base attack is scaled by <see cref="ResolveRageBuffPercent" /> of the rage gauge.
    ///     <para>
    ///         <b>DORMANT.</b> The shipped legacy build never enables this: the rage-buff state flag is only ever
    ///         assigned 0 in compiled code and the gauge is never written (its database load is commented out),
    ///         so the multiply is dead in the shipped cluster (contract dead-vestige edge case). Fenrir also
    ///         carries no rage-buff-STATE input on <see cref="ZoneContext" /> (only <c>RageGauge</c>), so the
    ///         gate can neither be satisfied nor threaded today -- reading the gauge alone must NOT fire the
    ///         multiply. This method therefore returns <paramref name="baseAttack" /> unchanged. The table above
    ///         is the fully-recoverable piece, ready for the day a rage-buff-state input plus a product decision
    ///         land (see openQuestions and wiringManifest -- do not wire this live without both).
    ///     </para>
    /// </summary>
    public static int ApplyRageAttackMultiplier(int baseAttack, ZoneContext zone)
    {
        if (!RageBuffStateActive)
            return baseAttack;

        // Reachable only once a real rage-buff-state gate exists; kept exact so the future wiring is a no-op edit.
        return ScaleByPercent(baseAttack, ResolveRageBuffPercent(zone.RageGauge));
    }
}

/// <summary>
///     The per-stat drunk multipliers for one drunk-potion id, as whole-number percentages (100 = unchanged),
///     consumed via <see cref="StatCalculator.ScaleByPercent" />. Réf. contract "per-id combat stat modifiers".
///     A value-type record so a lookup on the frozen table never touches the GC heap.
/// </summary>
public readonly record struct DrunkEffect(
    int MaxLifePercent = 100,
    int AttackPowerPercent = 100,
    int DefensePowerPercent = 100,
    int CriticalPercent = 100,
    int CriticalDefencePercent = 100,
    int AttackSuccessPercent = 100,
    int ElementAttackPercent = 100,
    int ElementDefensePercent = 100);
