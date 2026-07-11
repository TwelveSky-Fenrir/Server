using System.Numerics;
using Fenrir.Application.Game.Domain.World.Geometry;

namespace Fenrir.Application.Game.Domain.World.Pathfinding;

/// <summary>
///     Immutable triangle-connectivity graph over one zone's <c>.WM</c> navmesh (<see cref="ZoneGeometry" />),
///     built once per geometry and shareable/thread-safe to read. Two walkable floor triangles
///     (<see cref="WorldTriangle.PlaneInfo" /><c>.Y &gt; 0</c>, the same upward-facing cull
///     <see cref="ZoneGeometry.TryGetGroundHeight" /> applies) are adjacent iff they share an edge -- two
///     coincident vertex positions. The shared edge is retained as a "portal" (its two XZ endpoints) so the
///     funnel/string-pull step (<see cref="MonsterPathfinder" />) can tighten an A* triangle corridor into a
///     minimal set of turning points.
/// </summary>
/// <remarks>
///     This is a Fenrir-owned superset of legacy's own step-and-reject <c>mWORLD.Path</c>
///     (<c>Server/ts25zone/S09_MyWorld.cpp</c>), which never routed around obstacles; there is no legacy
///     byte-parity requirement for the graph or the search, only for the ground-snap/walkability primitives it
///     builds on (already in <see cref="ZoneGeometry" />).
///     <para>
///         Edge identity uses <b>exact</b> 3D <see cref="Vector3" /> equality, deliberately, not a quantized or
///         epsilon-tolerant key: the source mesh is a shared-index structure, so the two triangles either side
///         of a real edge carry bit-identical vertex positions for that edge. Full 3D (not XZ-projected)
///         equality is required so a bridge and the floor beneath it -- coincident in XZ but not in Y -- are
///         never treated as adjacent. Should a future mesh ever violate the shared-index guarantee (hairline
///         cracks from independently-authored vertices), the fix is to quantize <see cref="EdgeKey" /> to a
///         small world-unit grid; that is a deliberate, documented change, not a default.
///     </para>
///     <para>
///         Connectivity is stored in a compressed-sparse-row (CSR) layout -- <see cref="_adjacencyStart" /> plus
///         the parallel <see cref="_neighborTriangles" />/<see cref="_portalA" />/<see cref="_portalB" />
///         arrays -- so a triangle's neighbours iterate as a contiguous slice with no per-node
///         <see cref="List{T}" /> and no allocation on the search hot path.
///     </para>
/// </remarks>
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

    /// <summary>Total triangle count of the source mesh (walkable and not) -- the sizing basis for A* scratch.</summary>
    public int TriangleCount => _centroidsXz.Length;

    /// <summary>
    ///     Builds the connectivity graph for <paramref name="triangles" />. O(triangles), called once (lazily) per
    ///     geometry.
    /// </summary>
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

        // First pass: pair walkable triangles that share an exact 3D edge. Each directed adjacency is
        // accumulated once here (build-time allocation only), then packed into the CSR arrays below.
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

        // Second pass: CSR pack. Count per source triangle, prefix-sum into start offsets, then scatter.
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
            // Shared edge: connect both triangles, using the owner's already-projected XZ portal endpoints so
            // both directions reference identical portal geometry. A well-formed navmesh edge is shared by at
            // most two triangles; a non-manifold third+ triangle would simply pair with the same first owner,
            // which is harmless for monster routing (documented rather than special-cased).
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

    /// <summary>True if triangle <paramref name="triangleIndex" /> is a walkable floor (participates in the graph).</summary>
    public bool IsWalkable(int triangleIndex)
    {
        return _walkable[triangleIndex];
    }

    /// <summary>XZ centroid of triangle <paramref name="triangleIndex" /> -- the A* cost/heuristic reference point.</summary>
    public Vector2 CentroidXz(int triangleIndex)
    {
        return _centroidsXz[triangleIndex];
    }

    /// <summary>Inclusive lower bound of <paramref name="triangleIndex" />'s neighbour slice in the CSR arrays.</summary>
    public int NeighborStart(int triangleIndex)
    {
        return _adjacencyStart[triangleIndex];
    }

    /// <summary>Exclusive upper bound of <paramref name="triangleIndex" />'s neighbour slice in the CSR arrays.</summary>
    public int NeighborEnd(int triangleIndex)
    {
        return _adjacencyStart[triangleIndex + 1];
    }

    /// <summary>Neighbour triangle index at CSR slot <paramref name="slot" /> (between the start/end bounds above).</summary>
    public int NeighborAt(int slot)
    {
        return _neighborTriangles[slot];
    }

    /// <summary>True if <paramref name="a" /> and <paramref name="b" /> share an edge (both must be walkable).</summary>
    public bool AreAdjacent(int a, int b)
    {
        for (var slot = _adjacencyStart[a]; slot < _adjacencyStart[a + 1]; slot++)
            if (_neighborTriangles[slot] == b)
                return true;

        return false;
    }

    /// <summary>
    ///     The shared-edge portal (two XZ endpoints) crossed when travelling from <paramref name="fromTriangle" />
    ///     into <paramref name="toTriangle" /> -- the funnel's per-corridor-step portal.
    /// </summary>
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

    /// <summary>Order-independent exact-3D edge key; two triangles sharing an edge hash and compare equal.</summary>
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
