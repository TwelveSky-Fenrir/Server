using Fenrir.Application.Game.Domain.World.Geometry;

namespace Fenrir.Application.Game.Domain.Fishing;

public static class FishingCastResolver
{
    public static bool HasWaterAtCurrentPosition(ZoneGeometry? geometry, float x, float y, float z)
    {
        return geometry is not null &&
               geometry.TryGetGroundHeight(x, z, out _, y + 20f);
    }
}
