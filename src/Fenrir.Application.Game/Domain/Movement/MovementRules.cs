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
        var dx = intent.Location[0] - current.PosX;
        var dy = intent.Location[1] - current.PosY;
        var dz = intent.Location[2] - current.PosZ;
        var maxDistance = options.Value.MaxPlausibleMoveDistance;

        if (dx * dx + dy * dy + dz * dz > maxDistance * maxDistance)
            return false;

        if (geometry is null)
            return true;

        var targetY = intent.Location[1];

        geometry.Resolve(intent.Location[0], intent.Location[2], out var walkable, out var groundY);

        if (!walkable)
            return false;

        if (targetY < groundY - MaxBelowGroundTolerance)
            return false;

        return true;
    }
}
