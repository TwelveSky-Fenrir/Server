namespace Fenrir.Application.Game.Domain.Combat;

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

        var facingAngle = MathF.Atan2(sourceX - victimX, sourceZ - victimZ);

        return new AvatarDeathDirection(directionX, directionZ, facingAngle);
    }
}
