namespace Fenrir.Application.Game.Combat;

/// <summary>
///     Pure, side-effect-free arithmetic primitives shared by every attack-resolution path (report 05 §4,
///     verified directly against <c>Server/ts25zone/S07_MyGame02.cpp</c> -- <c>AttackPlayer</c> lines 886-1416
///     for PvP, <c>ProcessAttack03</c> lines 1825-2300+ for PvM, <c>ProcessAttack04</c> lines 3179-3330 for
///     MvP). Every method here mirrors ONE specific C++ expression 1:1, including its truncation points --
///     see <see cref="CombatResolver" /> for how these compose into a full attack.
/// </summary>
public static class CombatMath
{
    /// <summary>
    ///     Sentinel returned by <see cref="ComputeHitChancePercent" /> when the defender's block stat is &lt;= 0:
    ///     the legacy never even rolls in that case (the whole <c>if (tAttackBlockValue &gt; 0)</c> block is
    ///     skipped), so the attack always lands -- callers must treat "block &lt;= 0" as "always hit" themselves
    ///     rather than call this method at all in that case (see <see cref="CombatResolver" />).
    /// </summary>
    public const int AlwaysHitPercent = 100;

    /// <summary>
    ///     The legacy's asymmetric log-staircase hit-chance formula (report 05 §4 point 2), IDENTICAL across
    ///     PvP (<c>AttackPlayer</c> l.1024-1042) and PvM (<c>ProcessAttack03</c> l.2183-2202) and MvP
    ///     (<c>ProcessAttack04</c> l.3244-3262): base 70%, ±25% scaled by the ratio of the higher stat over the
    ///     lower, clamped to [1, 99]. Precondition: both <paramref name="attackSuccess" /> and
    ///     <paramref name="attackBlock" /> are &gt;= 1 (the caller already rejected an attacker with
    ///     <c>attackSuccess &lt; 1</c>, and a defender with <c>attackBlock &lt;= 0</c> never reaches this method
    ///     -- see <see cref="AlwaysHitPercent" />).
    /// </summary>
    public static int ComputeHitChancePercent(int attackSuccess, int attackBlock)
    {
        int determineValue;
        if (attackSuccess > attackBlock)
        {
            determineValue = (int)(70.0f + ((float)attackSuccess / attackBlock - 1.0f) * 25.0f);
            if (determineValue > 99) determineValue = 99;
        }
        else
        {
            determineValue = (int)(70.0f - ((float)attackBlock / attackSuccess - 1.0f) * 25.0f);
            if (determineValue < 1) determineValue = 1;
        }

        return determineValue;
    }

    /// <summary>One <c>rand_mir() % 100</c> draw compared against the hit chance -- true = the attack lands.</summary>
    public static bool RollHit(int hitChancePercent, IRandomSource rng)
    {
        return rng.NextInt32(100) < hitChancePercent;
    }

    /// <summary>
    ///     One <c>rand_mir() % 100</c> draw against a (already floor-clamped-to-0-by-the-caller) critical
    ///     chance percent -- true = critical (damage doubles). A chance &lt;= 0 never rolls (matches the
    ///     legacy's own <c>if (critical &gt; criticalDefense)</c> guard preceding every crit roll site).
    /// </summary>
    public static bool RollCritical(int criticalChancePercent, IRandomSource rng)
    {
        return criticalChancePercent > 0 && rng.NextInt32(100) < criticalChancePercent;
    }

    /// <summary>
    ///     The legacy's damage variance (report 05 §4 point 3, identical in <c>AttackPlayer</c> l.1097-1107 and
    ///     <c>ProcessAttack03</c> l.2242-2251 and <c>ProcessAttack04</c> l.3292-3300): TWO separate
    ///     <c>rand_mir()</c> draws -- one to pick add-vs-subtract (50/50), one for the 0-10% magnitude -- applied
    ///     to <paramref name="damage" /> and truncated via <c>(int)</c> exactly once.
    /// </summary>
    public static int ApplyVariance(int damage, IRandomSource rng)
    {
        var addsInsteadOfSubtracts = rng.NextInt32(2) == 0;
        var magnitudePercent = rng.NextInt32(11); // rand_mir() % 11 -> 0..10 inclusive
        var delta = (int)(damage * magnitudePercent * 0.01f);
        return addsInsteadOfSubtracts ? damage + delta : damage - delta;
    }

    /// <summary><c>CalDamage</c> (S07_MyGame02.cpp:572): a skill's attack-power-up ratio (percent) applied to a base power.</summary>
    public static int ApplySkillPowerRatio(int basePower, float ratioPercent)
    {
        return (int)(basePower * (ratioPercent + 100.0f) * 0.01f);
    }

    /// <summary><c>CheckInRange</c> (Header/mapcheck.h): full 3D squared-distance compare, no XZ-only shortcut.</summary>
    public static bool IsInRange(float x1, float y1, float z1, float x2, float y2, float z2, float maxDistance)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return dx * dx + dy * dy + dz * dz <= maxDistance * maxDistance;
    }
}
