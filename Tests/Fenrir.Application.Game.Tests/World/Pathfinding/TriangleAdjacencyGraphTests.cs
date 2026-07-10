using System.Numerics;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Pathfinding;

namespace Fenrir.Application.Game.Tests.World.Pathfinding;

/// <summary>
///     Covers <see cref="TriangleAdjacencyGraph" /> edge-sharing detection over a hand-built mesh: two triangles
///     that share an exact edge are adjacent (both directions), triangles that share no edge are not, and
///     non-walkable (<c>PlaneInfo.Y &lt;= 0</c>) triangles are excluded from the graph entirely.
/// </summary>
public class TriangleAdjacencyGraphTests
{
    // Upward-facing (walkable floor) and vertical (wall) plane coefficients (A, B, C, D) -- only the sign of B
    // (PlaneInfo.Y) matters to the graph's walkable cull.
    private static readonly Vector4 FloorPlane = new(0f, 1f, 0f, 10f);
    private static readonly Vector4 WallPlane = new(1f, 0f, 0f, 0f);

    private static WorldTriangle Floor(Vector3 a, Vector3 b, Vector3 c)
    {
        return new WorldTriangle(a, b, c, FloorPlane);
    }

    [Fact]
    public void TwoTrianglesSharingAnEdge_AreAdjacentBothWays()
    {
        // Unit square split into two floor triangles sharing the diagonal edge (10,10,0)-(0,10,10).
        var v00 = new Vector3(0, 10, 0);
        var v10 = new Vector3(10, 10, 0);
        var v11 = new Vector3(10, 10, 10);
        var v01 = new Vector3(0, 10, 10);

        var graph = TriangleAdjacencyGraph.Build([
            Floor(v00, v10, v11),
            Floor(v00, v11, v01)
        ]);

        Assert.True(graph.AreAdjacent(0, 1));
        Assert.True(graph.AreAdjacent(1, 0));
    }

    [Fact]
    public void TrianglesWithNoSharedEdge_AreNotAdjacent()
    {
        var near = Floor(new Vector3(0, 10, 0), new Vector3(10, 10, 0), new Vector3(0, 10, 10));
        var far = Floor(new Vector3(500, 10, 500), new Vector3(510, 10, 500), new Vector3(500, 10, 510));

        var graph = TriangleAdjacencyGraph.Build([near, far]);

        Assert.False(graph.AreAdjacent(0, 1));
        Assert.False(graph.AreAdjacent(1, 0));
        Assert.Equal(graph.NeighborStart(0), graph.NeighborEnd(0));
        Assert.Equal(graph.NeighborStart(1), graph.NeighborEnd(1));
    }

    [Fact]
    public void SharingASingleVertexButNoEdge_IsNotAdjacent()
    {
        // Both touch only the single point (10,10,0) -- one shared vertex is not a shared edge.
        var a = Floor(new Vector3(0, 10, 0), new Vector3(10, 10, 0), new Vector3(0, 10, 10));
        var b = Floor(new Vector3(10, 10, 0), new Vector3(20, 10, 0), new Vector3(20, 10, 10));

        var graph = TriangleAdjacencyGraph.Build([a, b]);

        Assert.False(graph.AreAdjacent(0, 1));
    }

    [Fact]
    public void NonWalkableTriangle_IsExcluded_EvenWhenItSharesAnEdge()
    {
        // A floor and a wall that share the edge (10,10,0)-(0,10,10): the wall (PlaneInfo.Y == 0) must never be
        // adjacent to the floor, and must itself have no neighbours.
        var v00 = new Vector3(0, 10, 0);
        var v10 = new Vector3(10, 10, 0);
        var v11 = new Vector3(10, 10, 10);
        var v01 = new Vector3(0, 10, 10);

        var floor = Floor(v00, v10, v11);
        var wall = new WorldTriangle(v00, v11, v01, WallPlane);

        var graph = TriangleAdjacencyGraph.Build([floor, wall]);

        Assert.False(graph.IsWalkable(1));
        Assert.False(graph.AreAdjacent(0, 1));
        Assert.False(graph.AreAdjacent(1, 0));
        Assert.Equal(graph.NeighborStart(0), graph.NeighborEnd(0));
        Assert.Equal(graph.NeighborStart(1), graph.NeighborEnd(1));
    }

    [Fact]
    public void SharedEdge_ExposesPortalEndpoints()
    {
        var v00 = new Vector3(0, 10, 0);
        var v10 = new Vector3(10, 10, 0);
        var v11 = new Vector3(10, 10, 10);
        var v01 = new Vector3(0, 10, 10);

        // Floor(v00,v10,v11) and Floor(v00,v11,v01) share the diagonal edge v00-v11 = (0,10,0)-(10,10,10).
        var graph = TriangleAdjacencyGraph.Build([
            Floor(v00, v10, v11),
            Floor(v00, v11, v01)
        ]);

        Assert.True(graph.TryGetPortal(0, 1, out var a, out var b));

        // The portal is the shared diagonal edge, projected to XZ (order-independent).
        var endpoints = new[] { a, b };
        Assert.Contains(new Vector2(0, 0), endpoints);
        Assert.Contains(new Vector2(10, 10), endpoints);
    }
}
