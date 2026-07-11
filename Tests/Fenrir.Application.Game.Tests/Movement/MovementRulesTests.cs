using System.Numerics;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Movement;

public class MovementRulesTests
{
    private static ZoneGeometry FlatSquareGeometry()
    {
        var planeInfo = new Vector4(0f, 1f, 0f, 10f);

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
        var state = StateAt(0f, 0f, 0f);

        Assert.True(rules.IsPlausible(state, MoveTo(600f, 0f, 0f)));
    }

    [Fact]
    public void Move_ExactlyAtBackstop_IsPlausible()
    {
        var rules = Rules();
        var state = StateAt(0f, 0f, 0f);

        Assert.True(rules.IsPlausible(state, MoveTo(666f, 0f, 0f)));
    }

    [Fact]
    public void Move_BeyondBackstop_IsRejected()
    {
        var rules = Rules();
        var state = StateAt(0f, 0f, 0f);

        Assert.False(rules.IsPlausible(state, MoveTo(700f, 0f, 0f)));
    }

    [Fact]
    public void Move_BackstopIs3D_IncludesTheYAxis()
    {
        var rules = Rules();
        var state = StateAt(0f, 0f, 0f);

        Assert.False(rules.IsPlausible(state, MoveTo(0f, 700f, 0f)));
    }

    [Fact]
    public void Move_OrdinaryWalkStep_IsPlausible()
    {
        var rules = Rules();
        var state = StateAt(30f, 10f, 20f);

        Assert.True(rules.IsPlausible(state, MoveTo(40f, 10f, 30f)));
    }

    [Fact]
    public void NoGeometry_DoesNotCheckTerrain_OnlyDistance()
    {
        var rules = Rules();
        var state = StateAt(30f, 10f, 20f);

        Assert.True(rules.IsPlausible(state, MoveTo(35f, 8f, 25f)));
    }

    [Fact]
    public void WithGeometry_TargetOnMeshAtCorrectHeight_IsPlausible()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var state = StateAt(30f, 10f, 20f);

        var intent = MoveTo(40f, 10f, 30f);

        Assert.True(rules.IsPlausible(state, intent, geometry));
    }

    [Fact]
    public void WithGeometry_TargetOffTheMesh_IsRejected_HorsMonde()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var state = StateAt(95f, 10f, 95f);

        var intent = MoveTo(105f, 10f, 105f);

        Assert.False(rules.IsPlausible(state, intent, geometry));
    }

    [Fact]
    public void WithGeometry_TargetBelowGround_IsRejected_SousLeTerrain()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var state = StateAt(30f, 10f, 20f);

        var intent = MoveTo(35f, -30f, 25f);

        Assert.False(rules.IsPlausible(state, intent, geometry));
    }

    [Fact]
    public void WithGeometry_BeyondBackstop_IsRejected_RegardlessOfTerrain()
    {
        var rules = Rules();
        var geometry = FlatSquareGeometry();
        var state = StateAt(0f, 10f, 0f);

        var intent = MoveTo(700f, 10f, 0f);

        Assert.False(rules.IsPlausible(state, intent, geometry));
    }
}
