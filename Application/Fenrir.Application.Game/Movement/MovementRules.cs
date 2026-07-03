using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Geometry;
using Fenrir.Contracts.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Movement;

/// <summary>
///     Anti-speed-hack gate (wire contract, architecture reference §6.5): a move is plausible only if the
///     straight-line distance it claims is coverable within the elapsed wall-clock time at
///     <see cref="GameServerOptions.MaxPlausibleSpeedPerSecond" />. Pure domain logic — no I/O, no session, so it
///     is trivially unit-testable and safe to call straight from the zone tick (§10.1's "Simulate" stage).
/// </summary>
public sealed class MovementRules(IOptions<GameServerOptions> options)
{
    /// <summary>
    ///     Small fixed allowance on top of the speed*time budget, so a move landing exactly on the limit (float rounding,
    ///     a slightly-late tick) is never rejected by a razor-thin margin.
    /// </summary>
    private const float SlackUnits = 1f;

    /// <summary>
    ///     How far BELOW the terrain's resolved ground height a claimed Y may sit before a target is rejected as
    ///     "sous le terrain" (M1 plan, Phase C/V1 item 1). No legacy source documents an equivalent server-side
    ///     check (the legacy client-authoritative navmesh walk never lets an in-bounds move claim a bogus Y in the
    ///     first place) -- this is a Fenrir-only anti-clip gate, same "generous, documented placeholder" posture as
    ///     <see cref="GameServerOptions.MaxPlausibleSpeedPerSecond" /> itself: wide enough to never reject a legit
    ///     slope/stair traversal within one network tick's worth of movement, narrow enough to catch an obviously
    ///     under-map/clipped target.
    /// </summary>
    private const float MaxBelowGroundTolerance = 20f;

    /// <summary>
    ///     True if <paramref name="intent" />'s target is coverable from <paramref name="current" /> within the
    ///     elapsed wall-clock time (speed gate, unconditional), AND — only when <paramref name="geometry" /> is
    ///     non-null, i.e. this zone's <c>.WM</c> file actually loaded (<see cref="World.Zone.Geometry" />) — the
    ///     target XZ is <see cref="ZoneGeometry.IsWalkable">walkable</see> and its claimed Y is not meaningfully
    ///     below the terrain's own <see cref="ZoneGeometry.TryGetGroundHeight">resolved ground height</see> there.
    ///     A move failing either the walkability or the below-ground check is rejected exactly like a
    ///     speed-implausible one — same resync mechanism, <c>Zone.HandleMove</c> does not distinguish why a
    ///     move was rejected.
    /// </summary>
    /// <remarks>
    ///     <paramref name="geometry" /> is null whenever no zone geometry is loaded (missing <c>.WM</c> file, a
    ///     documented, non-fatal gap — see <see cref="World.Zone.Geometry" />'s own remarks — or a unit test that
    ///     never wires one): in that case this method validates SPEED ONLY, unchanged from the original M1
    ///     behavior, never terrain. There is deliberately no "reject if geometry is missing" fallback — a zone
    ///     without terrain data must keep working exactly as before, not start rejecting every move.
    /// </remarks>
    public bool IsPlausible(PlayerRuntimeState current, in ActionInfo intent, DateTime nowUtc,
        ZoneGeometry? geometry = null)
    {
        var elapsedSeconds = (float)(nowUtc - current.LastMoveUtc).TotalSeconds;

        // A never-yet-moved player (elapsed <= 0, e.g. the very first move right after spawn) has no prior
        // timestamp to measure a SPEED budget from -- always plausible on that axis rather than dividing by a
        // zero/negative window. This short-circuits the speed check only: geometry (below) does not depend on
        // elapsed time and always still applies when loaded.
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

        // Hors-monde: no triangle's XZ footprint covers the target at all (legacy CheckPointInWorldWithoutYCoord).
        if (!geometry.IsWalkable(targetX, targetZ))
            return false;

        // No ground plane resolves either (e.g. only a vertical/downward-facing triangle sits under this XZ,
        // filtered out by TryGetGroundHeight's checkTwoSide:false default) -- same verdict as hors-monde.
        if (!geometry.TryGetGroundHeight(targetX, targetZ, out var groundY))
            return false;

        // Sous le terrain: the claimed Y sits meaningfully below the terrain surface actually underfoot.
        // Deliberately NOT symmetric (no "too far above ground" rejection) -- out of this item's stated scope,
        // and a hard ceiling would risk breaking legitimate multi-level platforms/bridges/jumps.
        if (targetY < groundY - MaxBelowGroundTolerance)
            return false;

        return true;
    }
}
