using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Core.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.Movement;

public sealed class MovementRules(IOptions<GameServerOptions> options)
{
    private const float MaxBelowGroundTolerance = 20f;

    public bool IsPlausible(PlayerRuntimeState current, in ActionInfo intent, ZoneGeometry? geometry = null)
    {
        return IsPlausible(current.PosX, current.PosY, current.PosZ,
            intent.Location[0], intent.Location[1], intent.Location[2], geometry);
    }

    public bool IsPlausible(float fromX, float fromY, float fromZ, float toX, float toY, float toZ,
        ZoneGeometry? geometry = null, float? maxDistanceOverride = null)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var dz = toZ - fromZ;
        var maxDistance = maxDistanceOverride ?? options.Value.MaxPlausibleMoveDistance;

        if (dx * dx + dy * dy + dz * dz > maxDistance * maxDistance)
            return false;

        if (geometry is null)
            return true;

        geometry.Resolve(toX, toZ, out var walkable, out var groundY);

        if (!walkable)
            return false;

        if (toY < groundY - MaxBelowGroundTolerance)
            return false;

        return true;
    }
}
