using System.Numerics;
using Fenrir.Application.Game.Domain.World.Geometry;

namespace Fenrir.Application.Game.Domain.World.Pathfinding;

/// <summary>
///     Per-zone A* + funnel (string-pull) navmesh router over one zone's <see cref="ZoneGeometry" />. A
///     deliberate Fenrir-owned superset of legacy's step-and-reject <c>mWORLD.Path</c>
///     (<c>Server/ts25zone/S09_MyWorld.cpp</c>) -- legacy never routed around obstacles, so there is no
///     byte-parity requirement here; only the walkability/ground-snap primitives this builds on are
///     legacy-cited (already in <see cref="ZoneGeometry" />).
/// </summary>
/// <remarks>
///     Owned by a single <see cref="Zone" /> and touched only on that zone's tick thread (single-writer
///     invariant) -- it therefore reuses all of its search scratch (the A* open set, the generation-marked
///     score/closed/came-from arrays, the funnel portal buffers) across calls with no per-call allocation.
///     Generation counters (<see cref="_scoreGeneration" />/<see cref="_closedGeneration" />) avoid clearing
///     the triangle-sized arrays each search: a stale entry from a previous search is recognised by a
///     non-matching generation and treated as unvisited.
///     <para>
///         A per-tick budget (<see cref="ResetBudget" />/<see cref="TryConsumeBudget" />) bounds how many full
///         path computations one zone performs per simulation pass; a monster denied budget this tick reuses its
///         cached waypoints or falls back to a straight-line step (see <see cref="Monsters.MonsterAiSystem" />),
///         never blocking the tick.
///     </para>
/// </remarks>
public sealed class MonsterPathfinder
{
    private readonly int[] _cameFrom;
    private readonly int[] _closedGeneration;
    private readonly ZoneGeometry _geometry;
    private readonly TriangleAdjacencyGraph _graph;
    private readonly float[] _gScore;
    private readonly int[] _scoreGeneration;

    private readonly PriorityQueue<int, float> _open = new();
    private readonly List<int> _pathTriangles = [];
    private readonly List<Vector2> _portalLeft = [];
    private readonly List<Vector2> _portalRight = [];
    private readonly List<Vector2> _apexScratch = [];

    private readonly int _perTickBudget;
    private int _budgetRemaining;
    private int _generation;

    public MonsterPathfinder(ZoneGeometry geometry, int perTickBudget)
    {
        _geometry = geometry;
        _graph = geometry.Navmesh;
        _perTickBudget = perTickBudget < 0 ? 0 : perTickBudget;
        _budgetRemaining = _perTickBudget;

        var triangleCount = _graph.TriangleCount;
        _gScore = new float[triangleCount];
        _cameFrom = new int[triangleCount];
        _scoreGeneration = new int[triangleCount];
        _closedGeneration = new int[triangleCount];
    }

    /// <summary>Restores this tick's path-computation budget -- called once per <see cref="Monsters.MonsterAiSystem" /> pass.</summary>
    public void ResetBudget()
    {
        _budgetRemaining = _perTickBudget;
    }

    /// <summary>
    ///     Claims one unit of this tick's path-computation budget. False once the budget is exhausted -- the
    ///     caller then reuses its cached path or steps straight-line, deferring a fresh computation to a later
    ///     tick rather than blocking.
    /// </summary>
    public bool TryConsumeBudget()
    {
        if (_budgetRemaining <= 0)
            return false;

        _budgetRemaining--;
        return true;
    }

    /// <summary>
    ///     Computes a route from <paramref name="from" /> to <paramref name="to" /> as a minimal sequence of XZ
    ///     turning-point waypoints (ending at the goal, excluding the start), reusing <paramref name="waypointsOut" />
    ///     (cleared then filled). Returns false -- caller falls back to a straight-line step -- when either
    ///     endpoint is off the navmesh or the goal triangle is unreachable from the start triangle. A start/goal
    ///     in the same triangle, or a corridor whose straight line is already clear, collapses to the single
    ///     direct waypoint (the funnel emits no interior corners).
    /// </summary>
    public bool TryFindPath(Vector3 from, Vector3 to, List<Vector2> waypointsOut)
    {
        waypointsOut.Clear();

        if (!_geometry.TryFindContainingWalkableTriangle(from.X, from.Z, out var startTriangle))
            return false;
        if (!_geometry.TryFindContainingWalkableTriangle(to.X, to.Z, out var goalTriangle))
            return false;

        var goalXz = new Vector2(to.X, to.Z);

        if (startTriangle == goalTriangle)
        {
            waypointsOut.Add(goalXz);
            return true;
        }

        if (!RunAStar(startTriangle, goalTriangle, goalXz))
            return false;

        Funnel(new Vector2(from.X, from.Z), goalXz, waypointsOut);
        return true;
    }

    /// <summary>
    ///     A* over the triangle graph. Edge cost is the XZ distance between triangle centroids; the heuristic is
    ///     the straight-line XZ distance from a triangle's centroid to the goal point. On success
    ///     <see cref="_pathTriangles" /> holds the start-to-goal triangle corridor.
    /// </summary>
    private bool RunAStar(int startTriangle, int goalTriangle, Vector2 goalXz)
    {
        var generation = ++_generation;
        _open.Clear();

        _gScore[startTriangle] = 0f;
        _scoreGeneration[startTriangle] = generation;
        _cameFrom[startTriangle] = -1;
        _open.Enqueue(startTriangle, Vector2.Distance(_graph.CentroidXz(startTriangle), goalXz));

        while (_open.TryDequeue(out var current, out _))
        {
            if (_closedGeneration[current] == generation)
                continue;
            _closedGeneration[current] = generation;

            if (current == goalTriangle)
            {
                ReconstructCorridor(goalTriangle);
                return true;
            }

            var currentCentroid = _graph.CentroidXz(current);
            var currentScore = _gScore[current];
            var end = _graph.NeighborEnd(current);
            for (var slot = _graph.NeighborStart(current); slot < end; slot++)
            {
                var neighbor = _graph.NeighborAt(slot);
                if (_closedGeneration[neighbor] == generation)
                    continue;

                var tentative = currentScore + Vector2.Distance(currentCentroid, _graph.CentroidXz(neighbor));
                if (_scoreGeneration[neighbor] == generation && tentative >= _gScore[neighbor])
                    continue;

                _gScore[neighbor] = tentative;
                _scoreGeneration[neighbor] = generation;
                _cameFrom[neighbor] = current;
                _open.Enqueue(neighbor, tentative + Vector2.Distance(_graph.CentroidXz(neighbor), goalXz));
            }
        }

        return false;
    }

    private void ReconstructCorridor(int goalTriangle)
    {
        _pathTriangles.Clear();
        for (var triangle = goalTriangle; triangle != -1; triangle = _cameFrom[triangle])
            _pathTriangles.Add(triangle);
        _pathTriangles.Reverse();
    }

    /// <summary>
    ///     Simple Stupid Funnel Algorithm (Mikko Mononen) over the shared-edge portals of the A* corridor: pulls
    ///     the string from <paramref name="from" /> to <paramref name="to" /> tight, emitting only the turning
    ///     points where the funnel actually bends. Left/right portal ordering is resolved per corridor step from
    ///     the travel direction between the two triangles' centroids, so the algorithm never depends on a
    ///     consistent mesh winding.
    /// </summary>
    private void Funnel(Vector2 from, Vector2 to, List<Vector2> waypointsOut)
    {
        BuildPortals(from, to);

        _apexScratch.Clear();
        var apex = _portalLeft[0];
        var left = _portalLeft[0];
        var right = _portalRight[0];
        int apexIndex = 0, leftIndex = 0, rightIndex = 0;
        _apexScratch.Add(apex);

        for (var i = 1; i < _portalLeft.Count; i++)
        {
            var candidateLeft = _portalLeft[i];
            var candidateRight = _portalRight[i];

            // Tighten the right side of the funnel.
            if (TriArea2(apex, right, candidateRight) <= 0f)
            {
                if (apex == right || TriArea2(apex, left, candidateRight) > 0f)
                {
                    right = candidateRight;
                    rightIndex = i;
                }
                else
                {
                    // Right over left: the left endpoint becomes the next corner; restart from there.
                    _apexScratch.Add(left);
                    apex = left;
                    apexIndex = leftIndex;
                    left = apex;
                    right = apex;
                    leftIndex = apexIndex;
                    rightIndex = apexIndex;
                    i = apexIndex;
                    continue;
                }
            }

            // Tighten the left side of the funnel.
            if (TriArea2(apex, left, candidateLeft) >= 0f)
            {
                if (apex == left || TriArea2(apex, right, candidateLeft) < 0f)
                {
                    left = candidateLeft;
                    leftIndex = i;
                }
                else
                {
                    // Left over right: the right endpoint becomes the next corner; restart from there.
                    _apexScratch.Add(right);
                    apex = right;
                    apexIndex = rightIndex;
                    left = apex;
                    right = apex;
                    leftIndex = apexIndex;
                    rightIndex = apexIndex;
                    i = apexIndex;
                    continue;
                }
            }
        }

        _apexScratch.Add(to);

        // Emit every corner after the start apex, de-duplicating exactly-coincident points. Exact equality is
        // correct here (no epsilon): the only coincidences the funnel produces come from direct assignment of
        // one point to another (the restart branches above, and start==first-portal endpoints), yielding
        // bit-identical values -- matching this codebase's no-epsilon geometry convention.
        for (var i = 1; i < _apexScratch.Count; i++)
        {
            var point = _apexScratch[i];
            if (waypointsOut.Count == 0 || waypointsOut[^1] != point)
                waypointsOut.Add(point);
        }

        if (waypointsOut.Count == 0)
            waypointsOut.Add(to);
    }

    private void BuildPortals(Vector2 from, Vector2 to)
    {
        _portalLeft.Clear();
        _portalRight.Clear();

        // Degenerate first portal: both sides are the start point.
        _portalLeft.Add(from);
        _portalRight.Add(from);

        for (var i = 0; i < _pathTriangles.Count - 1; i++)
        {
            var current = _pathTriangles[i];
            var next = _pathTriangles[i + 1];
            _graph.TryGetPortal(current, next, out var edgeA, out var edgeB);

            // Assign the edge's two endpoints to the funnel's left/right sides from the corridor travel
            // direction (current centroid -> next centroid). In the world XZ plane (X east, Z north), the funnel
            // convention below wants the RIGHT-hand endpoint on the side with a positive 2D cross product of
            // travel against (endpoint - origin), and the LEFT-hand endpoint on the other -- the mirror of the
            // screen-space (X, Y-down) orientation the textbook algorithm is usually written for.
            var travel = _graph.CentroidXz(next) - _graph.CentroidXz(current);
            var origin = _graph.CentroidXz(current);
            var crossA = travel.X * (edgeA.Y - origin.Y) - travel.Y * (edgeA.X - origin.X);
            if (crossA >= 0f)
            {
                _portalRight.Add(edgeA);
                _portalLeft.Add(edgeB);
            }
            else
            {
                _portalRight.Add(edgeB);
                _portalLeft.Add(edgeA);
            }
        }

        // Degenerate last portal: both sides are the goal point.
        _portalLeft.Add(to);
        _portalRight.Add(to);
    }

    /// <summary>Twice the signed area of triangle (a, b, c) in the XZ plane -- positive when c is left of a-&gt;b.</summary>
    private static float TriArea2(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
    }
}
