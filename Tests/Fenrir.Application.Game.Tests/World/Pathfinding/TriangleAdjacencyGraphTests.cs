using System.Numerics;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Pathfinding;

namespace Fenrir.Application.Game.Tests.World.Pathfinding;

public class TriangleAdjacencyGraphTests
{
    private static readonly Vector4 FloorPlane = new(0f, 1f, 0f, 10f);
    private static readonly Vector4 WallPlane = new(1f, 0f, 0f, 0f);

    private static WorldTriangle Floor(Vector3 a, Vector3 b, Vector3 c)
    {
        return new WorldTriangle(a, b, c, FloorPlane);
    }

    [Fact]
    public void TwoTrianglesSharingAnEdge_AreAdjacentBothWays()
    {
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
        var a = Floor(new Vector3(0, 10, 0), new Vector3(10, 10, 0), new Vector3(0, 10, 10));
        var b = Floor(new Vector3(10, 10, 0), new Vector3(20, 10, 0), new Vector3(20, 10, 10));

        var graph = TriangleAdjacencyGraph.Build([a, b]);

        Assert.False(graph.AreAdjacent(0, 1));
    }

    [Fact]
    public void NonWalkableTriangle_IsExcluded_EvenWhenItSharesAnEdge()
    {
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

        var graph = TriangleAdjacencyGraph.Build([
            Floor(v00, v10, v11),
            Floor(v00, v11, v01)
        ]);

        Assert.True(graph.TryGetPortal(0, 1, out var a, out var b));

        var endpoints = new[] { a, b };
        Assert.Contains(new Vector2(0, 0), endpoints);
        Assert.Contains(new Vector2(10, 10), endpoints);
    }
}
