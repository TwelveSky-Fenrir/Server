namespace Fenrir.Application.Game.Domain.Combat;

public static class WireHeading
{
    private const float RadiansToDegrees = 180f / MathF.PI;

    public static float FromDelta(float deltaX, float deltaZ)
    {
        if (deltaX == 0f && deltaZ == 0f)
            return 0f;

        var degrees = MathF.Atan2(deltaX, deltaZ) * RadiansToDegrees + 180f;
        return degrees >= 360f ? degrees - 360f : degrees;
    }

    public static float Between(float fromX, float fromZ, float toX, float toZ)
    {
        return FromDelta(toX - fromX, toZ - fromZ);
    }
}

public readonly record struct AvatarDeathDirection(float DirectionX, float DirectionZ, float FacingAngle)
{
    private const float ZeroClampDistance = 1f;

    public static AvatarDeathDirection FromPositions(float victimX, float victimZ, float sourceX, float sourceZ)
    {
        var dx = victimX - sourceX;
        var dz = victimZ - sourceZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);

        var (directionX, directionZ) = distance < ZeroClampDistance
            ? (0f, 0f)
            : (dx / distance, dz / distance);

        var facingAngle = WireHeading.Between(victimX, victimZ, sourceX, sourceZ);

        return new AvatarDeathDirection(directionX, directionZ, facingAngle);
    }
}
