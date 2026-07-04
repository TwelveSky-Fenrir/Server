using System.Numerics;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Geometry;
using Fenrir.Contracts.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Movement;

/// <summary>
///     Covers <see cref="MovementRules.IsPlausible" />'s terrain-aware branch: when a zone's <c>.WM</c> geometry is
///     loaded, a target off the navmesh or below the resolved ground height is rejected like a speed-implausible
///     move; a zone with no geometry keeps validating speed only.
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

    private static PlayerRuntimeState StateAt(float x, float y, float z, DateTime lastMoveUtc)
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
            LastMoveUtc = lastMoveUtc
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
    public void NoGeometry_OnlyValidatesSpeed_EvenForANonsensicalTargetY()
    {
        var rules = Rules();
        var now = DateTime.UtcNow;
        var state = StateAt(30f, 10f, 20f, now.AddSeconds(-1));

        // Y=-500 would be "sous le terrain" if any geometry were loaded -- with geometry=null (no .WM file, the
        // M1 default), only the small XZ step's speed is checked, and it passes: no regression from pre-V1.
        var intent = MoveTo(35f, -500f, 25f);

        Assert.True(rules.IsPlausible(state, in intent, now));
        Assert.True(rules.IsPlausible(state, in intent, now));
    }

    [Fact]
    public void WithGeometry_TargetOnMeshAtCorrectHeight_IsPlausible()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var now = DateTime.UtcNow;
        var state = StateAt(30f, 10f, 20f, now.AddSeconds(-1));

        var intent = MoveTo(40f, 10f, 30f); // inside the square, Y matches the resolved ground height exactly

        Assert.True(rules.IsPlausible(state, in intent, now, geometry));
    }

    [Fact]
    public void WithGeometry_TargetOffTheMesh_IsRejected_HorsMonde()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var now = DateTime.UtcNow;
        var state = StateAt(95f, 10f, 95f, now.AddSeconds(-1));

        // Just outside the 100x100 square -- a small, speed-plausible step, but off the navmesh entirely.
        var intent = MoveTo(105f, 10f, 105f);

        Assert.False(rules.IsPlausible(state, in intent, now, geometry));
    }

    [Fact]
    public void WithGeometry_TargetBelowGround_IsRejected_SousLeTerrain()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var now = DateTime.UtcNow;
        var state = StateAt(30f, 10f, 20f, now.AddSeconds(-1));

        // Inside the square (XZ), but claiming a Y far below the resolved ground height of 10 -- a clip/under-map target.
        var intent = MoveTo(35f, -500f, 25f);

        Assert.False(rules.IsPlausible(state, in intent, now, geometry));
    }

    [Fact]
    public void WithGeometry_SpeedImplausible_IsStillRejected_RegardlessOfTerrain()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var now = DateTime.UtcNow;
        var state = StateAt(30f, 10f, 20f, now.AddSeconds(-1));

        // On the mesh, correct height, but a teleport-scale jump -- the pre-existing speed gate still applies.
        var intent = MoveTo(99f, 10f, 99f);

        Assert.False(rules.IsPlausible(state, in intent, now, geometry));
    }
}
