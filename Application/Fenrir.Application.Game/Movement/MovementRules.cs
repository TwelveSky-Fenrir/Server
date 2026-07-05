using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Geometry;
using Fenrir.Network.Serialization.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Movement;

/// <summary>
///     Anti-speed-hack gate: a move is plausible only if the straight-line distance it claims is coverable
///     within the elapsed wall-clock time at <see cref="GameServerOptions.MaxPlausibleSpeedPerSecond" />.
/// </summary>
public sealed class MovementRules(IOptions<GameServerOptions> options)
{
    /// <summary>Fixed allowance on top of the speed*time budget so a move on the limit isn't rejected on rounding.</summary>
    private const float SlackUnits = 1f;

    /// <summary>
    ///     How far below the resolved ground height a claimed Y may sit before being rejected. Fenrir-only
    ///     anti-clip gate -- the legacy client-authoritative navmesh has no server-side equivalent.
    /// </summary>
    private const float MaxBelowGroundTolerance = 20f;

    /// <summary>
    ///     True if the move is speed-plausible, and -- only when <paramref name="geometry" /> is loaded -- the
    ///     target XZ is walkable and its Y isn't meaningfully below the terrain there.
    /// </summary>
    public bool IsPlausible(PlayerRuntimeState current, in ActionInfo intent, DateTime nowUtc,
        ZoneGeometry? geometry = null)
    {
        var elapsedSeconds = (float)(nowUtc - current.LastMoveUtc).TotalSeconds;

        // First move after spawn has no prior timestamp -- always plausible on the speed axis.
        if (elapsedSeconds > 0f)
        {
            var dx = intent.Location[0] - current.PosX;
            var dz = intent.Location[2] - current.PosZ;
            var distance = MathF.Sqrt(dx * dx + dz * dz);

            var budget = options.Value.MaxPlausibleSpeedPerSecond * elapsedSeconds + SlackUnits;
            if (distance > budget)
                return false;
        }

        if (geometry is null)
            return true;

        var targetX = intent.Location[0];
        var targetY = intent.Location[1];
        var targetZ = intent.Location[2];

        if (!geometry.IsWalkable(targetX, targetZ))
            return false;

        if (!geometry.TryGetGroundHeight(targetX, targetZ, out var groundY))
            return false;

        // No upper-bound check -- would risk breaking legitimate multi-level platforms/bridges/jumps.
        if (targetY < groundY - MaxBelowGroundTolerance)
            return false;

        return true;
    }
}
