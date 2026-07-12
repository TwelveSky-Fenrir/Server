using System.Numerics;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Pathfinding;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Pathfinding;

[Collection(AllocationRegressionCollection.Name)]
public class MonsterPathfinderMeshTetherTests
{
    private const float CellSize = 10f;
    private const float GroundY = 10f;
    private static readonly Vector4 FloorPlane = new(0f, 1f, 0f, GroundY);

    private static ZoneGeometry GridGeometry(params (int Col, int Row)[] cells)
    {
        var triangles = new List<WorldTriangle>(cells.Length * 2);
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;

        foreach (var (col, row) in cells)
        {
            var x0 = col * CellSize;
            var x1 = (col + 1) * CellSize;
            var z0 = row * CellSize;
            var z1 = (row + 1) * CellSize;

            var v00 = new Vector3(x0, GroundY, z0);
            var v10 = new Vector3(x1, GroundY, z0);
            var v11 = new Vector3(x1, GroundY, z1);
            var v01 = new Vector3(x0, GroundY, z1);

            triangles.Add(new WorldTriangle(v00, v10, v11, FloorPlane));
            triangles.Add(new WorldTriangle(v00, v11, v01, FloorPlane));

            minX = MathF.Min(minX, x0);
            minZ = MathF.Min(minZ, z0);
            maxX = MathF.Max(maxX, x1);
            maxZ = MathF.Max(maxZ, z1);
        }

        var indices = new int[triangles.Count];
        for (var i = 0; i < indices.Length; i++)
            indices[i] = i;

        var root = new QuadtreeNode(new Vector3(minX, 0, minZ), new Vector3(maxX, GroundY, maxZ), indices,
            [-1, -1, -1, -1]);

        return new ZoneGeometry(triangles.ToArray(), [root]);
    }

    private static ZoneGeometry OpenGround()
    {
        return GridGeometry(
            (0, 0), (1, 0), (2, 0),
            (0, 1), (1, 1), (2, 1),
            (0, 2), (1, 2), (2, 2));
    }

    private static ZoneGeometry GapWithTopDetour()
    {
        return GridGeometry(
            (0, 0), (0, 1), (0, 2),
            (2, 0), (2, 1), (2, 2),
            (1, 2));
    }


    [Fact]
    public void Clamped_DestinationAlreadyOnMesh_BehavesLikeOrdinaryPath()
    {
        var pathfinder = new MonsterPathfinder(OpenGround());
        var ordinary = new List<Vector2>();
        var clamped = new List<Vector2>();

        var from = new Vector3(5, GroundY, 5);
        var to = new Vector3(25, GroundY, 25);

        Assert.True(pathfinder.TryFindPath(from, to, ordinary));
        Assert.True(pathfinder.TryFindPathClamped(from, to, clamped));

        Assert.Equal(ordinary, clamped);
    }

    [Fact]
    public void Clamped_OffMeshDestination_ClampsToLastWalkablePointAlongApproach()
    {
        var pathfinder = new MonsterPathfinder(OpenGround());
        var waypoints = new List<Vector2>();

        var from = new Vector3(5, GroundY, 5);
        var to = new Vector3(50, GroundY, 50);

        var found = pathfinder.TryFindPathClamped(from, to, waypoints);

        Assert.True(found);
        Assert.NotEmpty(waypoints);

        var resolved = waypoints[^1];

        Assert.True(resolved.X < 45f, $"expected a point well short of the off-mesh goal, got {resolved.X}");
        Assert.True(resolved.X > 20f, $"expected meaningful progress toward the goal, got {resolved.X}");

        var geometry = OpenGround();
        Assert.True(geometry.TryFindContainingWalkableTriangle(resolved.X, resolved.Y, out _));
    }

    [Fact]
    public void Clamped_OffMeshStart_ReturnsFalseAndClearsOutput()
    {
        var pathfinder = new MonsterPathfinder(OpenGround());
        var waypoints = new List<Vector2> { new(1f, 2f) };

        var found = pathfinder.TryFindPathClamped(new Vector3(5000, GroundY, 5000), new Vector3(5, GroundY, 5),
            waypoints);

        Assert.False(found);
        Assert.Empty(waypoints);
    }

    [Fact]
    public void Clamped_BothEndpointsOnMesh_NoObstacle_StillCollapsesToDirectWaypoint()
    {
        var pathfinder = new MonsterPathfinder(OpenGround());
        var waypoints = new List<Vector2>();

        var found = pathfinder.TryFindPathClamped(new Vector3(5, GroundY, 5), new Vector3(25, GroundY, 25),
            waypoints);

        Assert.True(found);
        Assert.Single(waypoints);
        Assert.Equal(25f, waypoints[0].X, 3);
        Assert.Equal(25f, waypoints[0].Y, 3);
    }


    [Fact]
    public void Pursuit_StartAlreadyWithinTether_ReturnsTrueWithNoWaypoints()
    {
        var pathfinder = new MonsterPathfinder(OpenGround());
        var waypoints = new List<Vector2> { new(9f, 9f) };

        var from = new Vector3(5, GroundY, 5);
        var anchor = new Vector2(6f, 6f);
        var found = pathfinder.TryFindPursuitPath(from, new Vector3(25, GroundY, 25), anchor, 5f, waypoints);

        Assert.True(found);
        Assert.Empty(waypoints);
    }

    [Fact]
    public void Pursuit_DirectLine_ClipsAtExactTetherRadiusFromAnchor()
    {
        var pathfinder = new MonsterPathfinder(OpenGround());
        var waypoints = new List<Vector2>();

        var from = new Vector3(5, GroundY, 5);
        var to = new Vector3(25, GroundY, 25);
        var anchor = new Vector2(25f, 25f);
        const float tetherRadius = 10f;

        var found = pathfinder.TryFindPursuitPath(from, to, anchor, tetherRadius, waypoints);

        Assert.True(found);
        Assert.Single(waypoints);

        var resolved = waypoints[0];
        var distanceToAnchor = Vector2.Distance(resolved, anchor);
        Assert.Equal(tetherRadius, distanceToAnchor, 2);

        var totalDistance = Vector2.Distance(new Vector2(from.X, from.Z), anchor);
        var resolvedDistanceFromStart = Vector2.Distance(new Vector2(from.X, from.Z), resolved);
        Assert.True(resolvedDistanceFromStart < totalDistance);
    }

    [Fact]
    public void Pursuit_MultiCornerDetour_OnlyClipsTheFinalSegmentReachingTheAnchor()
    {
        var pathfinder = new MonsterPathfinder(GapWithTopDetour());
        var waypoints = new List<Vector2>();

        var from = new Vector3(7, GroundY, 3);
        var to = new Vector3(27, GroundY, 3);
        var anchor = new Vector2(27f, 3f);
        const float tetherRadius = 5f;

        var full = new List<Vector2>();
        Assert.True(pathfinder.TryFindPath(from, to, full));
        Assert.True(full.Count >= 2, "expected a multi-corner detour to set up this test");

        var found = pathfinder.TryFindPursuitPath(from, to, anchor, tetherRadius, waypoints);

        Assert.True(found);
        Assert.NotEmpty(waypoints);

        for (var i = 0; i < waypoints.Count - 1; i++)
            Assert.Equal(full[i], waypoints[i]);

        var resolved = waypoints[^1];
        Assert.Equal(tetherRadius, Vector2.Distance(resolved, anchor), 2);
    }

    [Fact]
    public void Pursuit_OffMeshGoal_RevertsWhollyAndReportsFailure_NoPartialCredit()
    {
        var pathfinder = new MonsterPathfinder(OpenGround());
        var waypoints = new List<Vector2> { new(1f, 2f) };

        var from = new Vector3(5, GroundY, 5);
        var to = new Vector3(-5000, GroundY, -5000);
        var anchor = new Vector2(0f, 0f);

        var found = pathfinder.TryFindPursuitPath(from, to, anchor, 5f, waypoints);

        Assert.False(found);
        Assert.Empty(waypoints);
    }

    [Fact]
    public void Pursuit_AnchorNeverEnteredByRoute_ReturnsFullUnclampedRoute()
    {
        var pathfinder = new MonsterPathfinder(OpenGround());
        var waypoints = new List<Vector2>();
        var ordinary = new List<Vector2>();

        var from = new Vector3(5, GroundY, 5);
        var to = new Vector3(25, GroundY, 25);
        var anchor = new Vector2(1000f, 1000f);

        Assert.True(pathfinder.TryFindPath(from, to, ordinary));
        var found = pathfinder.TryFindPursuitPath(from, to, anchor, 1f, waypoints);

        Assert.True(found);
        Assert.Equal(ordinary, waypoints);
    }

    [Fact]
    public void TryFindPursuitPath_ReusingBuffers_DoesNotAllocateOnTheHotPath()
    {
        var pathfinder = new MonsterPathfinder(GapWithTopDetour());
        var waypoints = new List<Vector2>();
        var from = new Vector3(7, GroundY, 3);
        var to = new Vector3(27, GroundY, 3);
        var anchor = new Vector2(27f, 3f);

        for (var i = 0; i < 64; i++)
            Assert.True(pathfinder.TryFindPursuitPath(from, to, anchor, 5f, waypoints));

        const int iterations = 500;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
            pathfinder.TryFindPursuitPath(from, to, anchor, 5f, waypoints);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < iterations * 8,
            $"expected ~0 allocation on the pursuit-tether hot path, got {allocated} bytes over {iterations} calls");
    }
}
