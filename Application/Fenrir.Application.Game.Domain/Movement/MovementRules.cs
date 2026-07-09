using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.Movement;

/// <summary>
///     Server-side sanity gate on an accepted CZ_AVATAR_ACTION_SEND (op15) position.
///     <para>
///         Legacy op15 movement is fully client-authoritative: the client-sent <c>r-&gt;tAction.aLocation</c> is
///         copied verbatim into <c>tUserInfo-&gt;mDATA.aAction</c> with NO speed or position validation
///         (<c>Server/ts25zone/S04_MyWork02.cpp:1770</c>; corroborated
///         <c>ServerDocs/12_ts25zone/04_MyWork02_PartieA.md:150-156</c>). The ONLY position sanity that ever
///         existed in that handler is a DISABLED ("# Defense Hack #", entirely commented out at
///         <c>S04_MyWork02.cpp:1738-1768</c>) distance check:
///         <c>fRange = mUTIL.ReturnLengthXYZ(r-&gt;tAction.aLocation, tUserInfo-&gt;mPRE_LOCATION); if (fRange &gt; 666.0f) { ...snap back to mPRE_LOCATION... }</c>
///         -- a 3D straight-line distance between the claimed new position and <c>mPRE_LOCATION</c>, compared
///         against a flat <c>666.0f</c> ceiling, with NO division by elapsed time (a distance cap, not a
///         units/second speed). <c>ReturnLengthXYZ</c> is the full 3-axis distance
///         (<c>Server/ts25zone/S07_MyGame03.cpp:5040-5043</c>). The snap-back correction in that same block is
///         likewise dead code.
///     </para>
///     <para>
///         Fenrir reinstates the two cited pieces of that check -- its 666-unit ceiling and its 3D
///         <c>ReturnLengthXYZ</c> formula -- as an active anti-teleport backstop. It deliberately does NOT copy
///         legacy's <c>mPRE_LOCATION</c> reference semantics: legacy only seeds <c>mPRE_LOCATION</c> on zone
///         entry (<c>S04_MyWork02.cpp:1054-1056</c>) and re-stamps it from op16 CZ_UPDATE_AVATAR_ACTION
///         (<c>:1880-1882</c>), never from the op15 move handler itself, so the disabled op15 check would have
///         measured every move against a stale, non-per-move reference (accumulating distance from the entry
///         point) -- a likely reason even 666 was abandoned. Fenrir instead compares against the last ACCEPTED
///         position (<paramref name="current" />, re-stamped on every accepted op15 move in
///         <c>Zone.HandleMove</c>), i.e. a true per-consecutive-move delta, which is the corrected, strictly
///         safer form of the same ceiling. Because legacy shipped the check disabled, this is strictly more
///         protective than legacy and never less permissive for legitimate play: a single accepted move may not
///         jump more than <see cref="GameServerOptions.MaxPlausibleMoveDistance" /> (default 666) units. It
///         deliberately does NOT reproduce the earlier Fenrir-invented units/second budget, which -- at the real
///         world-coordinate scale (a zone spans thousands of units) -- rejected ordinary walking and snapped
///         legitimate clients back on every step.
///     </para>
/// </summary>
public sealed class MovementRules(IOptions<GameServerOptions> options)
{
    /// <summary>
    ///     How far below the resolved ground height a claimed Y may sit before being rejected. Fenrir-only
    ///     anti-clip gate with NO legacy analogue -- legacy op15 never queries world geometry for a player move
    ///     at all (<c>mapcheck.h</c>, the only position-helper header the op15 handler includes, has no
    ///     walkability/height query; <c>WORLD_FOR_GXD</c> in <c>Server/ts25zone/S09_MyWorld.cpp</c> is used only
    ///     for server-authoritative NPC/summon placement and spawn-height resolution, never player-move
    ///     validation). Retained as deliberate Fenrir hardening and dormant while no <c>.WM</c> geometry is
    ///     loaded (the current deployment default) -- see the class remarks and
    ///     <see cref="IsPlausible" />'s own notes on the parity status of this branch.
    /// </summary>
    private const float MaxBelowGroundTolerance = 20f;

    /// <summary>
    ///     True if the move's straight-line 3D distance from <paramref name="current" />'s last accepted position
    ///     to <paramref name="intent" />'s claimed position is within
    ///     <see cref="GameServerOptions.MaxPlausibleMoveDistance" /> (the legacy 666-unit per-move ceiling), and
    ///     -- only when <paramref name="geometry" /> is loaded (Fenrir-only navmesh hardening with no legacy
    ///     analogue) -- the target XZ is walkable and its Y isn't meaningfully below the terrain there.
    /// </summary>
    public bool IsPlausible(PlayerRuntimeState current, in ActionInfo intent, ZoneGeometry? geometry = null)
    {
        // Per-consecutive-move distance cap, reusing legacy's disabled "# Defense Hack #" 666-unit ceiling and
        // its 3D ReturnLengthXYZ formula (see the class remarks for the mPRE_LOCATION reference-semantics
        // difference). current holds the last ACCEPTED position: HandleMove has not yet written the new one when
        // this runs. There is deliberately no "first move after spawn" exemption -- a fresh arrival's Pos is its
        // spawn/arrival coordinate and the client's first reported position is right there, so that move
        // measures a naturally small distance.
        var dx = intent.Location[0] - current.PosX;
        var dy = intent.Location[1] - current.PosY;
        var dz = intent.Location[2] - current.PosZ;
        var distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        if (distance > options.Value.MaxPlausibleMoveDistance)
            return false;

        if (geometry is null)
            return true;

        var targetX = intent.Location[0];
        var targetY = intent.Location[1];
        var targetZ = intent.Location[2];

        geometry.Resolve(targetX, targetZ, out var walkable, out var groundY);

        if (!walkable)
            return false;

        // No upper-bound check -- would risk breaking legitimate multi-level platforms/bridges/jumps.
        if (targetY < groundY - MaxBelowGroundTolerance)
            return false;

        return true;
    }
}
