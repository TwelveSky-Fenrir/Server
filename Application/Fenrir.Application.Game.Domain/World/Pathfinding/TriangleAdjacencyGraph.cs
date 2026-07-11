using System.Numerics;
using Fenrir.Application.Game.Domain.World.Geometry;

namespace Fenrir.Application.Game.Domain.World.Pathfinding;

public sealed class TriangleAdjacencyGraph
{
    private readonly int[] _adjacencyStart;
    private readonly Vector2[] _centroidsXz;
    private readonly int[] _neighborTriangles;
    private readonly Vector2[] _portalA;
    private readonly Vector2[] _portalB;
    private readonly bool[] _walkable;

    private TriangleAdjacencyGraph(int[] adjacencyStart, int[] neighborTriangles, Vector2[] portalA,
        Vector2[] portalB, Vector2[] centroidsXz, bool[] walkable)
    {
        _adjacencyStart = adjacencyStart;
        _neighborTriangles = neighborTriangles;
        _portalA = portalA;
        _portalB = portalB;
        _centroidsXz = centroidsXz;
        _walkable = walkable;
    }

        public int TriangleCount => _centroidsXz.Length;

        public static TriangleAdjacencyGraph Build(IReadOnlyList<WorldTriangle> triangles)
    {
        var count = triangles.Count;
        var walkable = new bool[count];
        var centroids = new Vector2[count];

        for (var i = 0; i < count; i++)
        {
            var triangle = triangles[i];
            walkable[i] = triangle.PlaneInfo.Y > 0f;
            if (walkable[i])
                centroids[i] = CentroidXz(triangle);
        }

        var edgeOwner = new Dictionary<EdgeKey, EdgeOwner>();
        var adjacencies = new List<DirectedAdjacency>();

        for (var t = 0; t < count; t++)
        {
            if (!walkable[t])
                continue;

            var triangle = triangles[t];
            AddEdge(edgeOwner, adjacencies, t, triangle.Vertex0, triangle.Vertex1);
            AddEdge(edgeOwner, adjacencies, t, triangle.Vertex1, triangle.Vertex2);
            AddEdge(edgeOwner, adjacencies, t, triangle.Vertex2, triangle.Vertex0);
        }

        var start = new int[count + 1];
        foreach (var adjacency in adjacencies)
            start[adjacency.From + 1]++;
        for (var i = 0; i < count; i++)
            start[i + 1] += start[i];

        var total = start[count];
        var neighborTriangles = new int[total];
        var portalA = new Vector2[total];
        var portalB = new Vector2[total];
        var cursor = new int[count];

        foreach (var adjacency in adjacencies)
        {
            var slot = start[adjacency.From] + cursor[adjacency.From];
            cursor[adjacency.From]++;
            neighborTriangles[slot] = adjacency.To;
            portalA[slot] = adjacency.PortalA;
            portalB[slot] = adjacency.PortalB;
        }

        return new TriangleAdjacencyGraph(start, neighborTriangles, portalA, portalB, centroids, walkable);
    }

    private static void AddEdge(Dictionary<EdgeKey, EdgeOwner> edgeOwner, List<DirectedAdjacency> adjacencies,
        int triangleIndex, Vector3 v0, Vector3 v1)
    {
        var key = EdgeKey.Of(v0, v1);
        var portalA = new Vector2(v0.X, v0.Z);
        var portalB = new Vector2(v1.X, v1.Z);

        if (edgeOwner.TryGetValue(key, out var owner))
        {
            adjacencies.Add(new DirectedAdjacency(owner.TriangleIndex, triangleIndex, owner.PortalA, owner.PortalB));
            adjacencies.Add(new DirectedAdjacency(triangleIndex, owner.TriangleIndex, owner.PortalA, owner.PortalB));
            return;
        }

        edgeOwner[key] = new EdgeOwner(triangleIndex, portalA, portalB);
    }

    private static Vector2 CentroidXz(in WorldTriangle triangle)
    {
        var x = (triangle.Vertex0.X + triangle.Vertex1.X + triangle.Vertex2.X) / 3f;
        var z = (triangle.Vertex0.Z + triangle.Vertex1.Z + triangle.Vertex2.Z) / 3f;
        return new Vector2(x, z);
    }

        public bool IsWalkable(int triangleIndex)
    {
        return _walkable[triangleIndex];
    }

        public Vector2 CentroidXz(int triangleIndex)
    {
        return _centroidsXz[triangleIndex];
    }

        public int NeighborStart(int triangleIndex)
    {
        return _adjacencyStart[triangleIndex];
    }

        public int NeighborEnd(int triangleIndex)
    {
        return _adjacencyStart[triangleIndex + 1];
    }

        public int NeighborAt(int slot)
    {
        return _neighborTriangles[slot];
    }

        public bool AreAdjacent(int a, int b)
    {
        for (var slot = _adjacencyStart[a]; slot < _adjacencyStart[a + 1]; slot++)
            if (_neighborTriangles[slot] == b)
                return true;

        return false;
    }

        public bool TryGetPortal(int fromTriangle, int toTriangle, out Vector2 a, out Vector2 b)
    {
        for (var slot = _adjacencyStart[fromTriangle]; slot < _adjacencyStart[fromTriangle + 1]; slot++)
            if (_neighborTriangles[slot] == toTriangle)
            {
                a = _portalA[slot];
                b = _portalB[slot];
                return true;
            }

        a = default;
        b = default;
        return false;
    }

        private readonly record struct EdgeKey(Vector3 First, Vector3 Second)
    {
        public static EdgeKey Of(Vector3 p, Vector3 q)
        {
            return Less(p, q) ? new EdgeKey(p, q) : new EdgeKey(q, p);
        }

        private static bool Less(Vector3 a, Vector3 b)
        {
            if (a.X != b.X) return a.X < b.X;
            if (a.Y != b.Y) return a.Y < b.Y;
            return a.Z < b.Z;
        }
    }

    private readonly record struct EdgeOwner(int TriangleIndex, Vector2 PortalA, Vector2 PortalB);

    private readonly record struct DirectedAdjacency(int From, int To, Vector2 PortalA, Vector2 PortalB);
}
