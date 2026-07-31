namespace Fenrir.Application.Game.Domain.Combat;

public static class WireHeading
{
    private const float RadiansToDegrees = 180f / MathF.PI;

    // GetYAngle (Server/Header/mapcheck.h:32) : le champ filaire aFront est en DEGRES [0,360[ avec une
    // phase de +180, et vaut exactement 0 -- pas 180 -- quand les deux points coincident (mapcheck.h:34).
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
