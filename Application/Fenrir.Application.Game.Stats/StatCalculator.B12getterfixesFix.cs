using System.Collections.Frozen;

namespace Fenrir.Application.Game.Stats;

/// <summary>
///     Workstream B12 getter-fix helpers for the MyFactor stat surface (the getters compiled only into
///     <c>ts25zone</c>). Each member here is a pure, directly-testable extraction of a correction the B12
///     behavior contract calls for. The fix itself is applied by a one-line call inserted into the relevant
///     private getter body (see the workstream's wiring manifest); pulling the correction logic into these
///     public helpers lets it be unit-tested in isolation, exactly as the RankBuff/StellarCore/TribeRole
///     contribution helpers on this same partial class already are, and keeps the getter-body edit a trivial,
///     low-risk one-liner for the serial integration pass.
/// </summary>
/// <remarks>
///     Réf. C++ : <c>Server/Header/Protocol/MyFactor.cpp</c> (the MyFactor stat getters) and
///     <c>Server/ts25zone/S07_MyGame03.cpp:9826-9891</c> (the Balance-Zone normalization tables). Scope notes:
///     <list type="bullet">
///         <item>
///             The FFA (free-for-all, zone 335) terminal override for <b>damage</b> (45000) and <b>defense</b>
///             (30000) is deliberately NOT re-modelled here: Fenrir already applies those at attack-resolution
///             time via <c>Fenrir.Application.Game.Domain.Combat.FfaEqualStatsOverride</c>. Only the
///             <b>max-life</b> (100000) and <b>max-mana</b> (30000) overrides -- which that type explicitly
///             leaves untouched, by its own remarks -- are provided here, closing the exact gap it anticipated.
///         </item>
///         <item>
///             The FFA <b>base-attribute</b> override (Vitality/Strength/Ki/Wisdom each forced to 1) is
///             unobservable through <see cref="EffectiveStats" />: those four intermediates only feed
///             max-life/max-mana/damage/defense, every one of which is itself hard-overridden inside zone 335,
///             so forcing them to 1 changes no reported stat. It is intentionally not wired.
///         </item>
///         <item>
///             The Balance-Zone normalization is a SEPARATE system, gated on a per-session stat-balance flag
///             that no current stat-input context carries. The pure tables are provided here so the eventual
///             wiring is a small step, but nothing calls them until that flag input lands (see open questions).
///         </item>
///     </list>
/// </remarks>
public static partial class StatCalculator
{
    // ---- Free-for-all (FFA) zone 335 terminal overrides ----
    // MyFactor.cpp:66-69 (FFA_CheckEqualStatsZone, a bare zone-number equality test), :99 (FFAMAPNUM=335),
    // :75-94 (the fixed FFA constant table). The override is a TERMINAL return applied AFTER the full
    // vitality/level/set/Phoenix/elixir computation, discarding it -- not a short-circuit before it.

    /// <summary>FFAMAPNUM -- the single free-for-all arena map (<c>MyFactor.cpp:66-69,99</c>).</summary>
    public const short FreeForAllZoneNumber = 335;

    /// <summary>
    ///     Fixed max-life every combatant reports inside the FFA arena (<c>MyFactor.cpp:2216-2221</c>, constants at
    ///     <c>:75-94</c>).
    /// </summary>
    public const int FreeForAllMaxLife = 100000;

    /// <summary>
    ///     Fixed max-mana every combatant reports inside the FFA arena (<c>MyFactor.cpp:2349-2354</c>, constants at
    ///     <c>:75-94</c>).
    /// </summary>
    public const int FreeForAllMaxMana = 30000;

    // ---- Balance-Zone stat normalization (Server/ts25zone/S07_MyGame03.cpp:9826-9891) ----
    // A SEPARATE, independent system from the FFA override, keyed on a different zone list. When the per-session
    // stat-balance flag is > 0 AND the current zone is a Balance-Zone, the four base-attribute getters REPLACE
    // the character's own stored attribute with a level-scaled constant BEFORE the equipment loop runs
    // (MyFactor.cpp:1727-1730 for Ki, :1845-1848 for Wisdom). Zone 335 is NOT in this list, so FFA and Balance
    // normalization can never both apply to the same zone. The pure tables are here; the getter wiring is
    // BLOCKED on a stat-balance-flag input no context carries yet (see open questions).

    private static readonly FrozenSet<short> BalanceStatZoneNumbers =
        new short[] { 19, 20, 21, 34, 49, 120, 154, 175, 176, 177, 190, 191, 192, 193 }.ToFrozenSet();

    /// <summary>
    ///     True inside the FFA arena (zone 335) -- a pure zone-number equality test with no other precondition
    ///     (not gated by tribe, level, gear, buffs, or session state), <c>MyFactor.cpp:66-69</c>.
    /// </summary>
    public static bool IsFreeForAllZone(short zoneNumber)
    {
        return zoneNumber == FreeForAllZoneNumber;
    }

    /// <summary>
    ///     Terminal hard override for <c>GetBaseMaxLife</c>: inside the FFA arena the whole computed max-life is
    ///     discarded and a fixed <see cref="FreeForAllMaxLife" /> is returned; every other zone returns
    ///     <paramref name="computedMaxLife" /> unchanged (<c>MyFactor.cpp:2216-2221</c>).
    /// </summary>
    public static int ApplyFreeForAllMaxLife(int computedMaxLife, short zoneNumber)
    {
        return IsFreeForAllZone(zoneNumber) ? FreeForAllMaxLife : computedMaxLife;
    }

    /// <summary>
    ///     Terminal hard override for <c>GetBaseMaxMana</c>: FFA arena returns a fixed
    ///     <see cref="FreeForAllMaxMana" />, every other zone returns <paramref name="computedMaxMana" />
    ///     unchanged (<c>MyFactor.cpp:2349-2354</c>).
    /// </summary>
    public static int ApplyFreeForAllMaxMana(int computedMaxMana, short zoneNumber)
    {
        return IsFreeForAllZone(zoneNumber) ? FreeForAllMaxMana : computedMaxMana;
    }

    // ---- Phoenix Amulet (pet-slot items 76005/76006/76007) damage SECOND pass ----

    /// <summary>
    ///     <c>GetBaseAttackPower</c>'s Phoenix "second pass" (<c>MyFactor.cpp:2699-2711</c>): keyed on the
    ///     slot-8 (EPET) item id, adds nothing for 76005, +1000 for 76006, +2000 for 76007. This sits on TOP of
    ///     the first-pass withdrawal-and-add (subtract the pet's own raw attack, add 3000/4000/5000) already in
    ///     <see cref="ComputeAttackPower" />, giving the true net damage delta of +3000/+5000/+7000 on top of
    ///     removing the raw pet attack. The legacy second pass has no pet-slot-occupancy guard, but this returns
    ///     0 for any non-Phoenix id, so calling it inside the existing occupancy guard is behaviourally
    ///     identical (a Phoenix id can only occupy an occupied slot).
    /// </summary>
    public static int PhoenixDamageSecondPassBonus(int petSlotItemId)
    {
        return PhoenixFlatBonus(petSlotItemId, 0, 1000, 2000);
    }

    /// <summary>
    ///     Whether <paramref name="zoneNumber" /> is a Balance-Zone (the legacy classifier returns 1 for these
    ///     fourteen ids, 0 for every other zone) -- <c>S07_MyGame03.cpp:9826-9848</c>.
    /// </summary>
    public static bool IsBalanceStatZone(short zoneNumber)
    {
        return BalanceStatZoneNumbers.Contains(zoneNumber);
    }

    /// <summary>
    ///     Balance-Zone level term, keyed on the raw base level: 112 or below returns 112; 113-145 returns 145;
    ///     146-156 returns 156; above 156 returns the actual level -- <c>S07_MyGame03.cpp:9850-9859</c>.
    /// </summary>
    public static int BalanceLevelTerm(int baseLevel)
    {
        return baseLevel switch
        {
            <= 112 => 112,
            <= 145 => 145,
            > 156 => baseLevel,
            _ => 156 // 146-156
        };
    }

    /// <summary>
    ///     Balance-Zone Vitality replacement: 112 or below returns 336; 113-145 returns 842; 146-156 returns
    ///     1420; above 156 returns 1 -- <c>S07_MyGame03.cpp:9861-9869</c>.
    /// </summary>
    public static int BalanceVitality(int baseLevel)
    {
        return baseLevel switch { <= 112 => 336, <= 145 => 842, > 156 => 1, _ => 1420 };
    }

    /// <summary>
    ///     Balance-Zone Strength replacement: 112 or below returns 351; 113-145 returns 886; 146-156 returns
    ///     1411; above 156 returns 1 -- <c>S07_MyGame03.cpp:9872-9881</c>.
    /// </summary>
    public static int BalanceStrength(int baseLevel)
    {
        return baseLevel switch { <= 112 => 351, <= 145 => 886, > 156 => 1, _ => 1411 };
    }

    /// <summary>
    ///     The full Balance-Zone base-attribute replacement set (Vitality/Strength/Intelligence/Dexterity) for a
    ///     given raw base level. Intelligence and Dexterity are always 1 at every level
    ///     (<c>S07_MyGame03.cpp:9883-9886</c>, <c>:9888-9891</c>); Vitality/Strength are level-scaled per
    ///     <see cref="BalanceVitality" />/<see cref="BalanceStrength" />. This is the value a Balance-Zone
    ///     getter substitutes for the character's own stored attributes before the equipment loop.
    /// </summary>
    public static (int Vitality, int Strength, int Intelligence, int Dexterity) BalanceAttributes(int baseLevel)
    {
        return (BalanceVitality(baseLevel), BalanceStrength(baseLevel), 1, 1);
    }
}
