using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- Stellar Core stat-table bonuses (WORKSTREAM B6) ----
    //
    // The legacy MyFactor base recompute folds a flat, additive Stellar-Core bonus into six independent derived
    // stats, each a PURE item-id -> value table lookup (no formula, no interpolation) keyed on the character's
    // currently-ACTIVATED core id (aStellarCoreNumber, STRUCT.h:788). That active id is 0 (none activated) or one
    // of exactly 28 legal ids -- tier-1 76527..76540 and tier-2 93500..93513 -- guaranteed by the decode/validity
    // path (IsValidStellarCore, function.h:2267-2303), never by these lookups. The active id already reaches this
    // calculator on CosmeticContext.StellarCoreNumber (assembled from PlayerRuntimeState.StellarCoreNumber in
    // EquipmentService.AssembleStatContexts). All six feed the BASE cache layer (recompute-on-event), never the
    // per-resolution effective layer.
    //
    // Legacy structure preserved verbatim (tables transcribed id-by-id, never derived):
    //   * ATTACK power and DEFENSE power share ONE byte-for-byte identical table
    //     (GetStellarDMG == GetStellarDEF, MyFactor.cpp:4675-4711 / 4713-4749). Tier-1 breaks its +50 step with a
    //     +150 jump at 76540 (900); tier-2 breaks its +125 step with a +375 jump at 93513 (2250).
    //   * ELEMENT attack and ELEMENT defense share ONE byte-for-byte identical table
    //     (GetStellarEDMG == GetStellarEDEF, MyFactor.cpp:4751-4787 / 4789-4825). Clean +5 tier-1, clean +125 tier-2.
    //   * CRITICAL defense uses a NARROWER table (GetStellarCRTDEF, MyFactor.cpp:4643-4673) that intentionally
    //     OMITS the three lowest ids of each band (76527/76528/76529 and 93500/93501/93502 -> 0, though they are
    //     otherwise fully valid cores) and breaks its "pairs" pattern to 6/8/10 at 93511/93512/93513.
    //   * MAX HP uses a TIER-2-ONLY table (inline switch in the base-max-HP accessor, MyFactor.cpp:2146-2162,
    //     clean +250 step 250..3500). Every tier-1 id grants zero HP; there is no default fall-through.
    //
    // Gate equivalence: the five stat getters are each invoked only under a "active id != 0" guard
    // (MyFactor.cpp:2691, 2948, 3601, 3800, 3961), and the HP switch is ungated. Because 0 is not a key in any
    // table below, a table lookup that returns 0 for id 0 reproduces both shapes identically -- the guard is
    // redundant, so each lookup is applied unconditionally here and yields 0 when no core is active.
    //
    // OPEN QUESTION (carried from the contract, unresolved -- see openQuestions): per-core expiry dates are stored
    // (STRUCT.h:559) but none of the cited decode/gate/table lines show an expiry check between "core is activated"
    // and "bonus is granted". Whether an expired-but-still-activated core keeps granting its bonuses is NOT
    // established; this port grants the bonus for any non-zero active id, matching every cited line, and does not
    // model an expiry gate. Flagged for cpp-ts25-explorer re-check.

    /// <summary>
    ///     GetStellarDMG == GetStellarDEF (MyFactor.cpp:4675-4711, byte-for-byte identical to 4713-4749). Shared by
    ///     the attack-power and defense-power contributions. Any id not present grants 0.
    /// </summary>
    private static readonly FrozenDictionary<int, int> StellarDamageDefenseBonusById = new Dictionary<int, int>
    {
        // Tier-1 (+50 step; +150 jump at 76540)
        [76527] = 50, [76528] = 150, [76529] = 200, [76530] = 300, [76531] = 350, [76532] = 400, [76533] = 450,
        [76534] = 500, [76535] = 550, [76536] = 600, [76537] = 650, [76538] = 700, [76539] = 750, [76540] = 900,
        // Tier-2 (+125 step; +375 jump at 93513)
        [93500] = 125, [93501] = 375, [93502] = 500, [93503] = 750, [93504] = 875, [93505] = 1000, [93506] = 1125,
        [93507] = 1250, [93508] = 1375, [93509] = 1500, [93510] = 1625, [93511] = 1750, [93512] = 1875, [93513] = 2250
    }.ToFrozenDictionary();

    /// <summary>
    ///     GetStellarEDMG == GetStellarEDEF (MyFactor.cpp:4751-4787, byte-for-byte identical to 4789-4825). Shared
    ///     by the element-attack and element-defense contributions. Any id not present grants 0.
    /// </summary>
    private static readonly FrozenDictionary<int, int> StellarElementBonusById = new Dictionary<int, int>
    {
        // Tier-1 (clean +5 step)
        [76527] = 5, [76528] = 10, [76529] = 15, [76530] = 20, [76531] = 25, [76532] = 30, [76533] = 35,
        [76534] = 40, [76535] = 45, [76536] = 50, [76537] = 55, [76538] = 60, [76539] = 65, [76540] = 70,
        // Tier-2 (clean +125 step)
        [93500] = 125, [93501] = 250, [93502] = 375, [93503] = 500, [93504] = 625, [93505] = 750, [93506] = 875,
        [93507] = 1000, [93508] = 1125, [93509] = 1250, [93510] = 1375, [93511] = 1500, [93512] = 1625, [93513] = 1750
    }.ToFrozenDictionary();

    /// <summary>
    ///     GetStellarCRTDEF (MyFactor.cpp:4643-4673). Deliberately narrower than the other tables: the three lowest
    ///     ids of each band (76527/76528/76529, 93500/93501/93502) have NO entry and grant 0 even though they are
    ///     valid cores. Tier-2 breaks its "pairs" pattern to 6/8/10 at 93511/93512/93513. Any id not present grants 0.
    /// </summary>
    private static readonly FrozenDictionary<int, int> StellarCriticalDefenceBonusById = new Dictionary<int, int>
    {
        // Tier-1 (starts at 76530; pairs 1,1 / 2,2 / 3,3 / 4,4 / 5,5 then 6)
        [76530] = 1, [76531] = 1, [76532] = 2, [76533] = 2, [76534] = 3, [76535] = 3,
        [76536] = 4, [76537] = 4, [76538] = 5, [76539] = 5, [76540] = 6,
        // Tier-2 (starts at 93503; pairs 1,1 / 2,2 / 3,3 / 4,4 then the 6/8/10 break)
        [93503] = 1, [93504] = 1, [93505] = 2, [93506] = 2, [93507] = 3, [93508] = 3,
        [93509] = 4, [93510] = 4, [93511] = 6, [93512] = 8, [93513] = 10
    }.ToFrozenDictionary();

    /// <summary>
    ///     The tier-2-only max-HP switch inside the base-max-HP accessor (MyFactor.cpp:2146-2162, clean +250 step).
    ///     There is no tier-1 entry: every core in 76527..76540 grants zero HP. Any id not present grants 0 (the
    ///     legacy switch has no default and leaves the running total untouched -- equivalent to +0 here).
    /// </summary>
    private static readonly FrozenDictionary<int, int> StellarMaxLifeBonusById = new Dictionary<int, int>
    {
        [93500] = 250, [93501] = 500, [93502] = 750, [93503] = 1000, [93504] = 1250, [93505] = 1500, [93506] = 1750,
        [93507] = 2000, [93508] = 2250, [93509] = 2500, [93510] = 2750, [93511] = 3000, [93512] = 3250, [93513] = 3500
    }.ToFrozenDictionary();

    private static int LookupStellarBonus(FrozenDictionary<int, int> table, int coreId)
    {
        return table.TryGetValue(coreId, out var value) ? value : 0;
    }

    /// <summary>
    ///     Attack-power Stellar-Core bonus (GetStellarDMG, MyFactor.cpp:2691-2692). Flat additive; feeds the BASE
    ///     attack-power getter. 0 when no valid core is active.
    /// </summary>
    public static int StellarCoreAttackPowerContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarDamageDefenseBonusById, cosmetic.StellarCoreNumber);
    }

    /// <summary>
    ///     Defense-power Stellar-Core bonus (GetStellarDEF, MyFactor.cpp:2948-2949). Shares the attack-power table
    ///     verbatim. Flat additive; feeds the BASE defense-power getter.
    /// </summary>
    public static int StellarCoreDefensePowerContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarDamageDefenseBonusById, cosmetic.StellarCoreNumber);
    }

    /// <summary>
    ///     Critical-defense Stellar-Core bonus (GetStellarCRTDEF, MyFactor.cpp:3601-3602). Uses the narrower
    ///     crit-defense table. Flat additive; feeds the BASE critical-defense getter.
    /// </summary>
    public static int StellarCoreCriticalDefenceContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarCriticalDefenceBonusById, cosmetic.StellarCoreNumber);
    }

    /// <summary>
    ///     Element-attack Stellar-Core bonus (GetStellarEDMG, MyFactor.cpp:3800-3801). Flat additive; feeds the
    ///     BASE element-attack getter.
    /// </summary>
    public static int StellarCoreElementAttackContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarElementBonusById, cosmetic.StellarCoreNumber);
    }

    /// <summary>
    ///     Element-defense Stellar-Core bonus (GetStellarEDEF, MyFactor.cpp:3961-3962). Shares the element-attack
    ///     table verbatim. Flat additive; feeds the BASE element-defense getter.
    /// </summary>
    public static int StellarCoreElementDefenseContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarElementBonusById, cosmetic.StellarCoreNumber);
    }

    /// <summary>
    ///     Max-HP Stellar-Core bonus (the tier-2-only switch, MyFactor.cpp:2146-2162). Flat additive; feeds the
    ///     BASE max-HP getter. Every tier-1 core grants 0 HP.
    /// </summary>
    public static int StellarCoreMaxLifeContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarMaxLifeBonusById, cosmetic.StellarCoreNumber);
    }
}
