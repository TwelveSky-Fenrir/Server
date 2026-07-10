using System.Numerics;
using Fenrir.Application.Game.Domain.World.Pathfinding;

namespace Fenrir.Application.Game.Domain.World.Geometry;

/// <summary>
///     Collision/height queries for one zone's <c>.WM</c> mesh; port of legacy <c>WORLD_FOR_GXD</c>
///     (<c>ts25zone/S09_MyWorld.cpp</c>). No epsilon in float comparisons, matching the original --
///     a tolerance would change which points are walkable at triangle edges.
/// </summary>
public sealed class ZoneGeometry(WorldTriangle[] triangles, QuadtreeNode[] quadtree)
{
    /// <summary>
    ///     The triangle-connectivity graph used by A* + funnel monster pathfinding (<see cref="MonsterPathfinder" />),
    ///     built lazily and once: a zone with geometry but no aggressive, actually-pathing monster never pays the
    ///     O(triangles) build cost. Thread-safe to first-touch (<see cref="Lazy{T}" />'s default publication mode),
    ///     though in practice only a zone's own tick thread reads it.
    /// </summary>
    private readonly Lazy<TriangleAdjacencyGraph> _navmesh = new(() => TriangleAdjacencyGraph.Build(triangles));

    public IReadOnlyList<WorldTriangle> Triangles => triangles;
    public IReadOnlyList<QuadtreeNode> Quadtree => quadtree;

    /// <summary>See <see cref="_navmesh" />.</summary>
    public TriangleAdjacencyGraph Navmesh => _navmesh.Value;

    /// <summary>
    ///     Ground height at (x, z) via quadtree descent (legacy <c>GetYCoord</c>). Among candidates under
    ///     <paramref name="ceiling" />, the highest wins, so multi-level platforms resolve to the surface underfoot.
    /// </summary>
    public bool TryGetGroundHeight(float x, float z, out float y, float? ceiling = null, bool checkTwoSide = false,
        bool firstHitOnly = false)
    {
        y = 0f;
        if (quadtree.Length == 0 || !TryDescend(x, z, out var nodeIndex))
            return false;

        var ceilingY = ceiling ?? quadtree[0].BoxMax.Y;
        var found = false;

        foreach (var triangleIndex in quadtree[nodeIndex].TriangleIndex)
        {
            var triangle = triangles[triangleIndex];
            if (!checkTwoSide && triangle.PlaneInfo.Y <= 0f)
                continue;

            if (!TryGetHeightFromPlane(triangle, x, z, out var candidateY))
                continue;

            if (candidateY > ceilingY)
                continue;

            if (!IsPointInsideTriangleXyz(triangle, x, candidateY, z))
                continue;

            if (!found)
            {
                found = true;
                y = candidateY;
                if (firstHitOnly) break;
            }
            else if (candidateY > y)
            {
                y = candidateY;
            }
        }

        return found;
    }

    /// <summary>
    ///     Walkability at (x, z); no Y parameter since legacy <c>CheckPointInWorldWithoutYCoord</c> never reads it either.
    /// </summary>
    public bool IsWalkable(float x, float z)
    {
        if (quadtree.Length == 0 || !TryDescend(x, z, out var nodeIndex))
            return false;

        foreach (var triangleIndex in quadtree[nodeIndex].TriangleIndex)
            if (IsPointInsideTriangleXz(triangles[triangleIndex], x, z))
                return true;

        return false;
    }

    /// <summary>
    ///     Index of a walkable floor triangle (<see cref="WorldTriangle.PlaneInfo" /><c>.Y &gt; 0</c>) whose XZ
    ///     footprint contains (x, z), via the same quadtree descent + XZ point-in-triangle test as
    ///     <see cref="IsWalkable" /> -- the on-mesh entry point A* pathfinding snaps a start/goal position to
    ///     (see <see cref="MonsterPathfinder" />). Returns the first matching walkable triangle in the resolved
    ///     leaf; on a multi-level footprint (e.g. a bridge over a floor, coincident in XZ) that first match may be
    ///     either surface, which is acceptable for grounding a monster's route -- callers needing a specific
    ///     surface use <see cref="TryGetGroundHeight" />'s height resolution instead. Excludes vertical
    ///     wall/ceiling triangles, matching the walkable set the adjacency graph is built from.
    /// </summary>
    public bool TryFindContainingWalkableTriangle(float x, float z, out int triangleIndex)
    {
        triangleIndex = -1;
        if (quadtree.Length == 0 || !TryDescend(x, z, out var nodeIndex))
            return false;

        foreach (var candidate in quadtree[nodeIndex].TriangleIndex)
        {
            var triangle = triangles[candidate];
            if (triangle.PlaneInfo.Y <= 0f)
                continue;

            if (IsPointInsideTriangleXz(triangle, x, z))
            {
                triangleIndex = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Combined <see cref="IsWalkable" /> + <see cref="TryGetGroundHeight" /> answer for (x, z) at
    ///     <see cref="TryGetGroundHeight" />'s default parameters (root ceiling, one-sided, best-of-many), descending
    ///     the quadtree once and scanning the resolved leaf's triangles once instead of the two independent descents
    ///     a caller needing both answers would otherwise perform. <paramref name="walkable" /> is true only when (x, z)
    ///     both sits inside some triangle's XZ footprint (matching <see cref="IsWalkable" />) and that footprint also
    ///     yields a resolvable one-sided ground height (matching <see cref="TryGetGroundHeight" />'s found result) --
    ///     the same conjunction a caller previously had to check across two separate calls; a footprint with no
    ///     standable surface underneath it (e.g. the underside of a bridge, with no floor triangle in that same XZ
    ///     spot) is not walkable under this combined answer even though <see cref="IsWalkable" /> alone would say yes.
    ///     <paramref name="groundY" /> is only meaningful when <paramref name="walkable" /> is true.
    /// </summary>
    public void Resolve(float x, float z, out bool walkable, out float groundY)
    {
        walkable = false;
        groundY = 0f;

        if (quadtree.Length == 0 || !TryDescend(x, z, out var nodeIndex))
            return;

        var hasFootprint = false;
        var hasGround = false;
        var ceilingY = quadtree[0].BoxMax.Y;

        foreach (var triangleIndex in quadtree[nodeIndex].TriangleIndex)
        {
            var triangle = triangles[triangleIndex];

            if (!hasFootprint && IsPointInsideTriangleXz(triangle, x, z))
                hasFootprint = true;

            if (triangle.PlaneInfo.Y <= 0f)
                continue;

            if (!TryGetHeightFromPlane(triangle, x, z, out var candidateY))
                continue;

            if (candidateY > ceilingY)
                continue;

            if (!IsPointInsideTriangleXyz(triangle, x, candidateY, z))
                continue;

            if (!hasGround || candidateY > groundY)
            {
                hasGround = true;
                groundY = candidateY;
            }
        }

        walkable = hasFootprint && hasGround;
    }

    private bool TryDescend(float x, float z, out int nodeIndex)
    {
        nodeIndex = 0;
        while (!quadtree[nodeIndex].IsLeaf)
        {
            var matched = false;
            foreach (var childIndex in quadtree[nodeIndex].ChildNodeIndex)
            {
                if (!quadtree[childIndex].ContainsXz(x, z)) continue;
                nodeIndex = childIndex;
                matched = true;
                break;
            }

            if (!matched) return false;
        }

        return true;
    }

    private static bool TryGetHeightFromPlane(in WorldTriangle triangle, float x, float z, out float y)
    {
        // B == 0 -> vertical (wall) triangle, no Y to solve for.
        if (triangle.PlaneInfo.Y == 0f)
        {
            y = 0f;
            return false;
        }

        y = (triangle.PlaneInfo.W - triangle.PlaneInfo.X * x - triangle.PlaneInfo.Z * z) / triangle.PlaneInfo.Y;
        return true;
    }

    /// <summary>Barycentric inside-triangle test in full 3D (legacy <c>CheckPointInTris</c>).</summary>
    private static bool IsPointInsideTriangleXyz(in WorldTriangle triangle, float x, float y, float z)
    {
        var edge0 = triangle.Vertex1 - triangle.Vertex0;
        var edge1 = triangle.Vertex2 - triangle.Vertex0;
        var toPoint = new Vector3(x, y, z) - triangle.Vertex0;

        var dot00 = Vector3.Dot(edge0, edge0);
        var dot01 = Vector3.Dot(edge0, edge1);
        var dot02 = Vector3.Dot(edge0, toPoint);
        var dot11 = Vector3.Dot(edge1, edge1);
        var dot12 = Vector3.Dot(edge1, toPoint);

        return TryResolveBarycentric(dot00, dot01, dot02, dot11, dot12);
    }

    /// <summary>Barycentric inside-triangle test projected onto XZ (legacy <c>CheckPointInTrisWithoutYCoord</c>).</summary>
    private static bool IsPointInsideTriangleXz(in WorldTriangle triangle, float x, float z)
    {
        var edge0 = new Vector2(triangle.Vertex1.X - triangle.Vertex0.X, triangle.Vertex1.Z - triangle.Vertex0.Z);
        var edge1 = new Vector2(triangle.Vertex2.X - triangle.Vertex0.X, triangle.Vertex2.Z - triangle.Vertex0.Z);
        var toPoint = new Vector2(x - triangle.Vertex0.X, z - triangle.Vertex0.Z);

        var dot00 = Vector2.Dot(edge0, edge0);
        var dot01 = Vector2.Dot(edge0, edge1);
        var dot02 = Vector2.Dot(edge0, toPoint);
        var dot11 = Vector2.Dot(edge1, edge1);
        var dot12 = Vector2.Dot(edge1, toPoint);

        return TryResolveBarycentric(dot00, dot01, dot02, dot11, dot12);
    }

    private static bool TryResolveBarycentric(float dot00, float dot01, float dot02, float dot11, float dot12)
    {
        var denominator = dot00 * dot11 - dot01 * dot01;
        if (denominator == 0f)
            return false;

        var inverseDenominator = 1f / denominator;
        var u = (dot11 * dot02 - dot01 * dot12) * inverseDenominator;
        if (u < 0f) return false;

        var v = (dot00 * dot12 - dot01 * dot02) * inverseDenominator;
        if (v < 0f) return false;

        return u + v <= 1f;
    }
}
