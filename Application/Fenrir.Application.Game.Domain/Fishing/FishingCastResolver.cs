using Fenrir.Application.Game.Domain.World.Geometry;

namespace Fenrir.Application.Game.Domain.Fishing;

/// <summary>
///     CZ_FISHING_STATE_SEND Sort=1 (cast) water check -- legacy
///     <c>
///         mWORLD.GetYCoord(x, z, &amp;y, TRUE,
///         y+20, FALSE, TRUE)
///     </c>
///     : any mesh triangle under the caster's own current position, within 20 units of
///     ceiling above it. No geometry loaded (missing .WM file) mirrors <c>!mCheckValidState</c> and fails.
/// </summary>
public static class FishingCastResolver
{
    public static bool HasWaterAtCurrentPosition(ZoneGeometry? geometry, float x, float y, float z)
    {
        return geometry is not null &&
               geometry.TryGetGroundHeight(x, z, out _, y + 20f, false, true);
    }
}
