using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Core.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.Movement;

public sealed class MovementRules(IOptions<GameServerOptions> options)
{
    public bool IsPlausible(PlayerRuntimeState current, in ActionInfo intent, ZoneGeometry geometry)
    {
        return IsPlausible(current.PosX, current.PosY, current.PosZ,
            intent.Location[0], intent.Location[1], intent.Location[2], geometry);
    }

    public bool IsPlausible(float fromX, float fromY, float fromZ, float toX, float toY, float toZ,
        ZoneGeometry geometry, float? maxDistanceOverride = null)
    {
        if (!float.IsFinite(fromX) || !float.IsFinite(fromY) || !float.IsFinite(fromZ) ||
            !float.IsFinite(toX) || !float.IsFinite(toY) || !float.IsFinite(toZ))
            return false;

        var dx = toX - fromX;
        var dy = toY - fromY;
        var dz = toZ - fromZ;
        var maxDistance = maxDistanceOverride ?? options.Value.MaxPlausibleMoveDistance;

        if (!float.IsFinite(maxDistance) || maxDistance <= 0f ||
            dx * dx + dy * dy + dz * dz > maxDistance * maxDistance)
            return false;

        return true;
    }
}
