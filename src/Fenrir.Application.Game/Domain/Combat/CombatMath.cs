namespace Fenrir.Application.Game.Domain.Combat;

public static class CombatMath
{
    public const int AlwaysHitPercent = 100;

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

    public static bool RollCritical(int criticalChancePercent, IRandomSource rng)
    {
        return criticalChancePercent > 0 && rng.NextInt32(100) < criticalChancePercent;
    }

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

    public static bool IsInRange(float x1, float y1, float z1, float x2, float y2, float z2, float maxDistance)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return dx * dx + dy * dy + dz * dz <= maxDistance * maxDistance;
    }
}
