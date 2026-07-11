using System.Numerics;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Pathfinding;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Pathfinding;

/// <summary>
///     Covers the two <see cref="MonsterPathfinder" /> additions from behavior contract <c>A2-mesh-tether</c>:
///     <see cref="MonsterPathfinder.TryFindPathClamped" /> (wander/patrol off-mesh-destination clamp) and
///     <see cref="MonsterPathfinder.TryFindPursuitPath" /> (pursuit-tether early-stop). Separate file from
///     <c>MonsterPathfinderTests</c> so a concurrent sibling working on the pre-existing A* + funnel coverage in
///     that file never collides with this one.
/// </summary>
/// <remarks>
///     Joins the serialized <see cref="AllocationRegressionCollection" /> for the same reason
///     <c>MonsterPathfinderTests</c> does -- <see cref="TryFindPursuitPath_ReusingBuffers_DoesNotAllocateOnTheHotPath" />
///     reads a per-thread <see cref="GC.GetAllocatedBytesForCurrentThread" /> delta that concurrent test execution
///     perturbs.
/// </remarks>
[Collection(AllocationRegressionCollection.Name)]
public class MonsterPathfinderMeshTetherTests
{
    private const float CellSize = 10f;
    private const float GroundY = 10f;
    private static readonly Vector4 FloorPlane = new(0f, 1f, 0f, GroundY);

    /// <summary>Same grid-mesh builder shape as <c>MonsterPathfinderTests.GridGeometry</c> (kept file-local -- no shared test navmesh helper exists yet).</summary>
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

    /// <summary>Single 3x3-cell open square spanning [0,30]x[0,30].</summary>
    private static ZoneGeometry OpenGround()
    {
        return GridGeometry(
            (0, 0), (1, 0), (2, 0),
            (0, 1), (1, 1), (2, 1),
            (0, 2), (1, 2), (2, 2));
    }

    /// <summary>Two vertical columns joined only by a top connector, forcing an up-and-over detour.</summary>
    private static ZoneGeometry GapWithTopDetour()
    {
        return GridGeometry(
            (0, 0), (0, 1), (0, 2),
            (2, 0), (2, 1), (2, 2),
            (1, 2));
    }

    // ---- TryFindPathClamped ------------------------------------------------------------------------------

    [Fact]
    public void Clamped_DestinationAlreadyOnMesh_BehavesLikeOrdinaryPath()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
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
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2>();

        var from = new Vector3(5, GroundY, 5);
        var to = new Vector3(50, GroundY, 50); // far outside the [0,30]x[0,30] mesh

        var found = pathfinder.TryFindPathClamped(from, to, waypoints);

        Assert.True(found);
        Assert.NotEmpty(waypoints);

        var resolved = waypoints[^1];

        // Clamped well short of the original off-mesh destination, but still materially past the start --
        // the boundary bisection should land near the grid's far edge (x=z=30 along this 45-degree approach).
        Assert.True(resolved.X < 45f, $"expected a point well short of the off-mesh goal, got {resolved.X}");
        Assert.True(resolved.X > 20f, $"expected meaningful progress toward the goal, got {resolved.X}");

        // The resolved point must itself be walkable -- the whole point of the clamp.
        var geometry = OpenGround();
        Assert.True(geometry.TryFindContainingWalkableTriangle(resolved.X, resolved.Y, out _));
    }

    [Fact]
    public void Clamped_OffMeshStart_ReturnsFalseAndClearsOutput()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2> { new(1f, 2f) };

        var found = pathfinder.TryFindPathClamped(new Vector3(5000, GroundY, 5000), new Vector3(5, GroundY, 5),
            waypoints);

        Assert.False(found);
        Assert.Empty(waypoints);
    }

    [Fact]
    public void Clamped_BothEndpointsOnMesh_NoObstacle_StillCollapsesToDirectWaypoint()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2>();

        var found = pathfinder.TryFindPathClamped(new Vector3(5, GroundY, 5), new Vector3(25, GroundY, 25),
            waypoints);

        Assert.True(found);
        Assert.Single(waypoints);
        Assert.Equal(25f, waypoints[0].X, 3);
        Assert.Equal(25f, waypoints[0].Y, 3);
    }

    // ---- TryFindPursuitPath -------------------------------------------------------------------------------

    [Fact]
    public void Pursuit_StartAlreadyWithinTether_ReturnsTrueWithNoWaypoints()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2> { new(9f, 9f) }; // pre-populated to prove it is cleared

        var from = new Vector3(5, GroundY, 5);
        var anchor = new Vector2(6f, 6f); // distance ~1.41 from `from`
        var found = pathfinder.TryFindPursuitPath(from, new Vector3(25, GroundY, 25), anchor, 5f, waypoints);

        Assert.True(found);
        Assert.Empty(waypoints);
    }

    [Fact]
    public void Pursuit_DirectLine_ClipsAtExactTetherRadiusFromAnchor()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2>();

        var from = new Vector3(5, GroundY, 5);
        var to = new Vector3(25, GroundY, 25);
        var anchor = new Vector2(25f, 25f); // the chased avatar sits at the destination itself
        const float tetherRadius = 10f;

        var found = pathfinder.TryFindPursuitPath(from, to, anchor, tetherRadius, waypoints);

        Assert.True(found);
        Assert.Single(waypoints);

        var resolved = waypoints[0];
        var distanceToAnchor = Vector2.Distance(resolved, anchor);
        Assert.Equal(tetherRadius, distanceToAnchor, 2);

        // The clipped point must still lie strictly between the start and the raw destination, not overshoot it.
        var totalDistance = Vector2.Distance(new Vector2(from.X, from.Z), anchor);
        var resolvedDistanceFromStart = Vector2.Distance(new Vector2(from.X, from.Z), resolved);
        Assert.True(resolvedDistanceFromStart < totalDistance);
    }

    [Fact]
    public void Pursuit_MultiCornerDetour_OnlyClipsTheFinalSegmentReachingTheAnchor()
    {
        var pathfinder = new MonsterPathfinder(GapWithTopDetour(), 24);
        var waypoints = new List<Vector2>();

        var from = new Vector3(7, GroundY, 3); // cell (0,0)
        var to = new Vector3(27, GroundY, 3); // cell (2,0) -- reachable only via the top detour
        var anchor = new Vector2(27f, 3f); // sits at the goal
        const float tetherRadius = 5f;

        var full = new List<Vector2>();
        Assert.True(pathfinder.TryFindPath(from, to, full));
        Assert.True(full.Count >= 2, "expected a multi-corner detour to set up this test");

        var found = pathfinder.TryFindPursuitPath(from, to, anchor, tetherRadius, waypoints);

        Assert.True(found);
        Assert.NotEmpty(waypoints);

        // Every corner strictly before the final one is untouched (none of them sit within the small tether
        // radius of the far-away anchor), and only the last, clipped point is new.
        for (var i = 0; i < waypoints.Count - 1; i++)
            Assert.Equal(full[i], waypoints[i]);

        var resolved = waypoints[^1];
        Assert.Equal(tetherRadius, Vector2.Distance(resolved, anchor), 2);
    }

    [Fact]
    public void Pursuit_OffMeshGoal_RevertsWhollyAndReportsFailure_NoPartialCredit()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2> { new(1f, 2f) };

        var from = new Vector3(5, GroundY, 5);
        var to = new Vector3(-5000, GroundY, -5000); // off-mesh
        var anchor = new Vector2(0f, 0f);

        var found = pathfinder.TryFindPursuitPath(from, to, anchor, 5f, waypoints);

        Assert.False(found);
        Assert.Empty(waypoints);
    }

    [Fact]
    public void Pursuit_AnchorNeverEnteredByRoute_ReturnsFullUnclampedRoute()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2>();
        var ordinary = new List<Vector2>();

        var from = new Vector3(5, GroundY, 5);
        var to = new Vector3(25, GroundY, 25);
        var anchor = new Vector2(1000f, 1000f); // nowhere near the route

        Assert.True(pathfinder.TryFindPath(from, to, ordinary));
        var found = pathfinder.TryFindPursuitPath(from, to, anchor, 1f, waypoints);

        Assert.True(found);
        Assert.Equal(ordinary, waypoints);
    }

    [Fact]
    public void TryFindPursuitPath_ReusingBuffers_DoesNotAllocateOnTheHotPath()
    {
        var pathfinder = new MonsterPathfinder(GapWithTopDetour(), 24);
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
