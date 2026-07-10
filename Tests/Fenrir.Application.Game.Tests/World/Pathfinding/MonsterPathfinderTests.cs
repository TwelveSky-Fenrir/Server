using System.Numerics;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Pathfinding;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Pathfinding;

/// <summary>
///     Covers <see cref="MonsterPathfinder" /> A* + funnel routing over hand-built navmeshes: a direct funnel on
///     open ground, an obstacle-avoiding detour around a gap, same-triangle/off-mesh edge cases, the per-tick
///     budget, and allocation-freedom of the search hot path.
/// </summary>
/// <remarks>
///     Joins the serialized <see cref="AllocationRegressionCollection" /> because
///     <see cref="TryFindPath_ReusingBuffers_DoesNotAllocateOnTheHotPath" /> reads a per-thread
///     <see cref="GC.GetAllocatedBytesForCurrentThread" /> delta that concurrent test execution perturbs (JIT and
///     thread-pool bookkeeping landing on the measuring thread) -- see that collection's own remarks.
/// </remarks>
[Collection(AllocationRegressionCollection.Name)]
public class MonsterPathfinderTests
{
    private const float CellSize = 10f;
    private const float GroundY = 10f;
    private static readonly Vector4 FloorPlane = new(0f, 1f, 0f, GroundY);

    /// <summary>
    ///     Builds a navmesh from a set of present grid cells: each cell <c>(col, row)</c> spans
    ///     <c>[col*10,(col+1)*10] x [row*10,(row+1)*10]</c> at <see cref="GroundY" />, split into two floor
    ///     triangles along the cell diagonal so orthogonally-adjacent present cells share exact triangle edges.
    ///     A single-leaf quadtree over the whole bounding box backs the (x, z) containment lookups.
    /// </summary>
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

    /// <summary>Single 3x3-cell open square, so any start/goal within it is trivially connected.</summary>
    private static ZoneGeometry OpenGround()
    {
        return GridGeometry(
            (0, 0), (1, 0), (2, 0),
            (0, 1), (1, 1), (2, 1),
            (0, 2), (1, 2), (2, 2));
    }

    /// <summary>
    ///     Two vertical columns joined only by a top connector; the two-cell-tall gap
    ///     (<c>(1,0)</c>/<c>(1,1)</c>) blocks any straight line between the columns' bottoms, forcing an
    ///     up-and-over detour.
    /// </summary>
    private static ZoneGeometry GapWithTopDetour()
    {
        return GridGeometry(
            (0, 0), (0, 1), (0, 2),
            (2, 0), (2, 1), (2, 2),
            (1, 2));
    }

    private static float PathLength(Vector2 from, List<Vector2> waypoints)
    {
        var total = 0f;
        var previous = from;
        foreach (var waypoint in waypoints)
        {
            total += Vector2.Distance(previous, waypoint);
            previous = waypoint;
        }

        return total;
    }

    [Fact]
    public void OpenGround_StraightLineClear_YieldsDirectPath()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2>();

        var found = pathfinder.TryFindPath(new Vector3(5, GroundY, 5), new Vector3(25, GroundY, 25), waypoints);

        Assert.True(found);
        // A clear line across open ground collapses to the single goal waypoint (the funnel emits no corners).
        Assert.Single(waypoints);
        Assert.Equal(25f, waypoints[0].X, 3);
        Assert.Equal(25f, waypoints[0].Y, 3);
    }

    [Fact]
    public void SameTriangle_YieldsSingleDirectWaypoint()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2>();

        // Both points sit below the diagonal of cell (0,0) -- the same triangle.
        var found = pathfinder.TryFindPath(new Vector3(7, GroundY, 3), new Vector3(8, GroundY, 4), waypoints);

        Assert.True(found);
        Assert.Single(waypoints);
        Assert.Equal(8f, waypoints[0].X, 3);
        Assert.Equal(4f, waypoints[0].Y, 3);
    }

    [Fact]
    public void ObstacleBetweenEndpoints_RoutesAroundIt()
    {
        var pathfinder = new MonsterPathfinder(GapWithTopDetour(), 24);
        var waypoints = new List<Vector2>();

        var from = new Vector3(7, GroundY, 3); // cell (0,0), below its diagonal
        var to = new Vector3(27, GroundY, 3); // cell (2,0), below its diagonal
        var found = pathfinder.TryFindPath(from, to, waypoints);

        Assert.True(found);

        // Not a direct path: it has to turn to get around the gap.
        Assert.True(waypoints.Count >= 2, $"expected a detour with corners, got {waypoints.Count} waypoint(s)");

        // It routes UP and over the gap -- some corner is well above both endpoints' z (=3).
        Assert.Contains(waypoints, w => w.Y >= 15f);

        // The routed length is far longer than the (blocked) straight-line distance of 20 units.
        var straightLine = Vector2.Distance(new Vector2(from.X, from.Z), new Vector2(to.X, to.Z));
        Assert.True(PathLength(new Vector2(from.X, from.Z), waypoints) > straightLine + 15f,
            "routed path should be substantially longer than the blocked straight line");

        // Ends at the goal.
        Assert.Equal(27f, waypoints[^1].X, 2);
        Assert.Equal(3f, waypoints[^1].Y, 2);
    }

    [Fact]
    public void OffMeshStart_ReturnsFalse_AndClearsOutput()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2> { new(1f, 2f) }; // pre-populated to prove it is cleared

        var found = pathfinder.TryFindPath(new Vector3(5000, GroundY, 5000), new Vector3(5, GroundY, 5), waypoints);

        Assert.False(found);
        Assert.Empty(waypoints);
    }

    [Fact]
    public void OffMeshGoal_ReturnsFalse()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 24);
        var waypoints = new List<Vector2>();

        var found = pathfinder.TryFindPath(new Vector3(5, GroundY, 5), new Vector3(-5000, GroundY, -5000), waypoints);

        Assert.False(found);
        Assert.Empty(waypoints);
    }

    [Fact]
    public void DisconnectedIslands_ReturnFalse()
    {
        // Two present cells with no shared edge and a gap between them: reachable containment, unreachable route.
        var geometry = GridGeometry((0, 0), (5, 5));
        var pathfinder = new MonsterPathfinder(geometry, 24);
        var waypoints = new List<Vector2>();

        var found = pathfinder.TryFindPath(new Vector3(5, GroundY, 5), new Vector3(55, GroundY, 55), waypoints);

        Assert.False(found);
    }

    [Fact]
    public void Budget_ExhaustsThenResets()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 2);

        Assert.True(pathfinder.TryConsumeBudget());
        Assert.True(pathfinder.TryConsumeBudget());
        Assert.False(pathfinder.TryConsumeBudget());

        pathfinder.ResetBudget();
        Assert.True(pathfinder.TryConsumeBudget());
    }

    [Fact]
    public void ZeroBudget_NeverGrantsAComputation()
    {
        var pathfinder = new MonsterPathfinder(OpenGround(), 0);

        Assert.False(pathfinder.TryConsumeBudget());
        pathfinder.ResetBudget();
        Assert.False(pathfinder.TryConsumeBudget());
    }

    [Fact]
    public void TryFindPath_ReusingBuffers_DoesNotAllocateOnTheHotPath()
    {
        var pathfinder = new MonsterPathfinder(GapWithTopDetour(), 24);
        var waypoints = new List<Vector2>();
        var from = new Vector3(7, GroundY, 3);
        var to = new Vector3(27, GroundY, 3);

        // Warm up well past tiered-compilation's tier-1 promotion (~30 calls) so no re-JIT lands inside the
        // measured window, and so every reused A*/funnel scratch buffer (and the lazy navmesh adjacency) has
        // already grown to its steady-state capacity before the first measured call.
        for (var i = 0; i < 64; i++)
            Assert.True(pathfinder.TryFindPath(from, to, waypoints));

        const int iterations = 500;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
            pathfinder.TryFindPath(from, to, waypoints);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Scratch is fully reused once warmed, so the search allocates nothing per call. The threshold is an
        // average of under 8 bytes/call -- a real per-call allocation (a List/PriorityQueue grow or a fresh
        // result object) is dozens of bytes or more, so this still catches any regression, while leaving headroom
        // for the occasional stray byte of runtime bookkeeping the per-thread counter attributes to this thread.
        Assert.True(allocated < iterations * 8,
            $"expected ~0 allocation on the pathfinding hot path, got {allocated} bytes over {iterations} calls");
    }
}
