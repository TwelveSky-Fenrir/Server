using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public readonly record struct MonsterKnockbackVector(float X, float Z)
{
    public static readonly MonsterKnockbackVector Zero = new(0f, 0f);

    public bool IsZero => X == 0f && Z == 0f;
}

public static class MonsterDeathKnockback
{
    public const byte StationaryDamageType = 1;

    public const float PointBlankDistanceThreshold = 1f;

    public const float ShortMagnitude = 1f;

    public const float LongMagnitudeA = 4f;

    public const float LongMagnitudeB = 3.7f;

    public static MonsterKnockbackVector Compute(float killerX, float killerZ, float monsterX, float monsterZ,
        byte monsterDamageType, bool isCriticalHit, IRandomSource random)
    {
        if (monsterDamageType == StationaryDamageType)
            return MonsterKnockbackVector.Zero;

        var dx = monsterX - killerX;
        var dz = monsterZ - killerZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);

        if (distance <= PointBlankDistanceThreshold)
            return MonsterKnockbackVector.Zero;

        var unitX = dx / distance;
        var unitZ = dz / distance;

        var magnitude = ResolveMagnitude(isCriticalHit, random);
        return new MonsterKnockbackVector(unitX * magnitude, unitZ * magnitude);
    }

    private static float ResolveMagnitude(bool isCriticalHit, IRandomSource random)
    {
        if (isCriticalHit)
            return ShortMagnitude;

        return random.NextInt32(4) switch
        {
            0 or 1 => ShortMagnitude,
            2 => LongMagnitudeA,
            _ => LongMagnitudeB
        };
    }
}
