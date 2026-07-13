namespace Fenrir.Application.Game.Domain.Commerce;

public static class PersonalShopRegionPolicy
{
    private const float Radius = 1000f;
    private const float RadiusSquared = Radius * Radius;

    private static readonly Region[] Regions =
    [
        new(1, 4f, 0f, -2f, 0),
        new(6, -189f, 0f, 1150f, 1),
        new(11, 449f, 1f, 439f, 2),
        new(140, 452f, 0f, 487f, 3),
        new(37, 1f, 0f, -1478f, null)
    ];

    public static bool IsWithinPermittedRegion(short mapId, byte tribe, float x, float y, float z)
    {
        foreach (var region in Regions)
        {
            if (region.MapId != mapId)
                continue;

            if (region.RequiredTribe is { } requiredTribe && tribe != requiredTribe)
                return false;

            var dx = x - region.X;
            var dy = y - region.Y;
            var dz = z - region.Z;
            return dx * dx + dy * dy + dz * dz < RadiusSquared;
        }

        return false;
    }

    private readonly record struct Region(short MapId, float X, float Y, float Z, byte? RequiredTribe);
}
