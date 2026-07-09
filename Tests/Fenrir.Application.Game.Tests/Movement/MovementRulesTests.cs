using System.Numerics;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Movement;

/// <summary>
///     Covers <see cref="MovementRules.IsPlausible" />:
///     <list type="bullet">
///         <item>
///             the per-move distance backstop -- a single accepted move may not jump more than
///             <see cref="GameServerOptions.MaxPlausibleMoveDistance" /> (default 666) units, in 3D, from the
///             last accepted position, reinstating the legacy op15 handler's DISABLED
///             <c>ReturnLengthXYZ(aLocation, mPRE_LOCATION) &gt; 666.0f</c> "# Defense Hack #" check
///             (<c>Server/ts25zone/S04_MyWork02.cpp:1738-1768</c>; 3D per
///             <c>Server/ts25zone/S07_MyGame03.cpp:5040-5043</c>); and
///         </item>
///         <item>
///             the terrain-aware branch (Fenrir-only hardening, no legacy analogue): when a zone's <c>.WM</c>
///             geometry is loaded, a target off the navmesh or below the resolved ground height is rejected; a
///             zone with no geometry validates distance only.
///         </item>
///     </list>
/// </summary>
public class MovementRulesTests
{
    /// <summary>
    ///     A flat 100x100 ground square (Y=10) split into two triangles; the quadtree root is a single leaf, so
    ///     "out of the mesh" is modeled by per-triangle containment, not quadtree box culling -- matching
    ///     <c>CheckPointInWorldWithoutYCoord</c>.
    /// </summary>
    private static ZoneGeometry FlatSquareGeometry()
    {
        var planeInfo = new Vector4(0f, 1f, 0f, 10f); // 0*x + 1*y + 0*z = 10 -> horizontal plane at Y=10

        var triangles = new[]
        {
            new WorldTriangle(new Vector3(0, 10, 0), new Vector3(100, 10, 0), new Vector3(100, 10, 100), planeInfo),
            new WorldTriangle(new Vector3(0, 10, 0), new Vector3(100, 10, 100), new Vector3(0, 10, 100), planeInfo)
        };

        var root = new QuadtreeNode(new Vector3(0, 0, 0), new Vector3(100, 10, 100), [0, 1], [-1, -1, -1, -1]);

        return new ZoneGeometry(triangles, [root]);
    }

    private static MovementRules Rules()
    {
        return new MovementRules(Options.Create(new GameServerOptions()));
    }

    private static PlayerRuntimeState StateAt(float x, float y, float z)
    {
        return new PlayerRuntimeState
        {
            CharacterId = 1,
            Session = ZoneTestKit.CreateSession(1).Session,
            Name = "Hero",
            Tribe = 1,
            Gender = 0,
            HeadType = 2,
            FaceType = 3,
            Level = 42,
            PosX = x,
            PosY = y,
            PosZ = z,
            LastMoveUtc = DateTime.UtcNow
        };
    }

    private static ActionInfo MoveTo(float x, float y, float z)
    {
        return new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [x, y, z],
            TargetLocation = [x, y, z],
            Front = 0f,
            TargetFront = 0f,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };
    }

    [Fact]
    public void Move_WellWithinBackstop_IsPlausible()
    {
        var rules = Rules();
        // 600 units on the X axis -- under the 666 ceiling, so a legitimate (if long) single step is accepted.
        // The earlier units/second budget would have rejected this on any realistic elapsed time; the legacy
        // never rejected below 666 units, and shipped even that check disabled.
        var state = StateAt(0f, 0f, 0f);

        Assert.True(rules.IsPlausible(state, MoveTo(600f, 0f, 0f)));
    }

    [Fact]
    public void Move_ExactlyAtBackstop_IsPlausible()
    {
        var rules = Rules();
        // Legacy compared fRange > 666.0f (strictly greater), so a move of exactly the ceiling is accepted.
        var state = StateAt(0f, 0f, 0f);

        Assert.True(rules.IsPlausible(state, MoveTo(666f, 0f, 0f)));
    }

    [Fact]
    public void Move_BeyondBackstop_IsRejected()
    {
        var rules = Rules();
        // 700 units in one packet -- past the 666 ceiling, a gross teleport-scale jump. Rejected.
        var state = StateAt(0f, 0f, 0f);

        Assert.False(rules.IsPlausible(state, MoveTo(700f, 0f, 0f)));
    }

    [Fact]
    public void Move_BackstopIs3D_IncludesTheYAxis()
    {
        var rules = Rules();
        // Pure vertical jump of 700 units: ReturnLengthXYZ is 3D (S07_MyGame03.cpp:5040-5043), so Y counts
        // toward the distance and this is rejected exactly like a 700-unit horizontal jump.
        var state = StateAt(0f, 0f, 0f);

        Assert.False(rules.IsPlausible(state, MoveTo(0f, 700f, 0f)));
    }

    [Fact]
    public void Move_OrdinaryWalkStep_IsPlausible()
    {
        var rules = Rules();
        // A ~14-unit step -- the common case. Always accepted; the whole point of the fix is that ordinary
        // movement never trips the backstop regardless of how fast the packets arrive.
        var state = StateAt(30f, 10f, 20f);

        Assert.True(rules.IsPlausible(state, MoveTo(40f, 10f, 30f)));
    }

    [Fact]
    public void NoGeometry_DoesNotCheckTerrain_OnlyDistance()
    {
        var rules = Rules();
        var state = StateAt(30f, 10f, 20f);

        // A small XZ step to a nonsensical Y=8 (2 below the notional ground) -- with geometry=null (no .WM file,
        // the M1 default) there is no terrain check at all, and the 3D distance is tiny, so it passes.
        Assert.True(rules.IsPlausible(state, MoveTo(35f, 8f, 25f)));
    }

    [Fact]
    public void WithGeometry_TargetOnMeshAtCorrectHeight_IsPlausible()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var state = StateAt(30f, 10f, 20f);

        var intent = MoveTo(40f, 10f, 30f); // inside the square, Y matches the resolved ground height exactly

        Assert.True(rules.IsPlausible(state, intent, geometry));
    }

    [Fact]
    public void WithGeometry_TargetOffTheMesh_IsRejected_HorsMonde()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var state = StateAt(95f, 10f, 95f);

        // Just outside the 100x100 square -- a small, distance-plausible step, but off the navmesh entirely.
        var intent = MoveTo(105f, 10f, 105f);

        Assert.False(rules.IsPlausible(state, intent, geometry));
    }

    [Fact]
    public void WithGeometry_TargetBelowGround_IsRejected_SousLeTerrain()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var state = StateAt(30f, 10f, 20f);

        // Inside the square (XZ), a small step, but claiming a Y well below the resolved ground height of 10 --
        // a clip/under-map target. The 3D distance stays under 666, so it is the terrain branch (not the
        // distance backstop) that rejects this.
        var intent = MoveTo(35f, -30f, 25f);

        Assert.False(rules.IsPlausible(state, intent, geometry));
    }

    [Fact]
    public void WithGeometry_BeyondBackstop_IsRejected_RegardlessOfTerrain()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var state = StateAt(0f, 10f, 0f);

        // A 700-unit teleport: the distance backstop rejects it before the terrain branch is ever consulted
        // (the target is also off the 100-unit mesh, but the distance check short-circuits first).
        var intent = MoveTo(700f, 10f, 0f);

        Assert.False(rules.IsPlausible(state, intent, geometry));
    }
}
