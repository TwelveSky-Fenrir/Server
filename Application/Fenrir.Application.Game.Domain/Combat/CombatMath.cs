namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Pure attack-resolution arithmetic, verified 1:1 against S07_MyGame02.cpp's
///     AttackPlayer/ProcessAttack03/ProcessAttack04.
/// </summary>
public static class CombatMath
{
    /// <summary>
    ///     Defender block &lt;= 0: legacy skips the roll entirely, so attack always lands -- callers must not call this
    ///     method in that case.
    /// </summary>
    public const int AlwaysHitPercent = 100;

    /// <summary>Base 70% ±25% scaled by success/block ratio, clamped [1,99]. Both args must be &gt;= 1.</summary>
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

    public static bool RollHit(int hitChancePercent, IRandomSource rng)
    {
        return rng.NextInt32(100) < hitChancePercent;
    }

    /// <summary>Chance &lt;= 0 never rolls, matching the legacy's guard before every crit roll site.</summary>
    public static bool RollCritical(int criticalChancePercent, IRandomSource rng)
    {
        return criticalChancePercent > 0 && rng.NextInt32(100) < criticalChancePercent;
    }

    /// <summary>Two separate rolls: add-vs-subtract (50/50), then 0-10% magnitude; truncated once.</summary>
    public static int ApplyVariance(int damage, IRandomSource rng)
    {
        var addsInsteadOfSubtracts = rng.NextInt32(2) == 0;
        var magnitudePercent = rng.NextInt32(11);
        var delta = (int)(damage * magnitudePercent * 0.01f);
        return addsInsteadOfSubtracts ? damage + delta : damage - delta;
    }

    public static int ApplySkillPowerRatio(int basePower, float ratioPercent)
    {
        return (int)(basePower * (ratioPercent + 100.0f) * 0.01f);
    }

    /// <summary>Full 3D squared-distance compare -- no XZ-only shortcut.</summary>
    public static bool IsInRange(float x1, float y1, float z1, float x2, float y2, float z2, float maxDistance)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return dx * dx + dy * dy + dz * dz <= maxDistance * maxDistance;
    }
}
