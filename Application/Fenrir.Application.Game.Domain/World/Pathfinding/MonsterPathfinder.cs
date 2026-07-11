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
    /// <summary>
    ///     Bisection iteration count for <see cref="ClampToMeshBoundary" />: resolution to 1/2^16 (~0.00002) of
    ///     the from-to segment length, several orders tighter than any monster placement precision this codebase
    ///     works in, in O(1) fixed work with no Fenrir-chosen world-unit step-size constant to tune.
    /// </summary>
    private const int BoundaryBisectionIterations = 16;

    private readonly List<Vector2> _apexScratch = [];
    private readonly int[] _cameFrom;
    private readonly int[] _closedGeneration;
    private readonly ZoneGeometry _geometry;
    private readonly TriangleAdjacencyGraph _graph;
    private readonly float[] _gScore;

    private readonly PriorityQueue<int, float> _open = new();
    private readonly List<int> _pathTriangles = [];

    private readonly int _perTickBudget;
    private readonly List<Vector2> _portalLeft = [];
    private readonly List<Vector2> _portalRight = [];
    private readonly int[] _scoreGeneration;

    /// <summary>Scratch buffer for the unclamped route <see cref="TryFindPursuitPath" /> clips against the tether circle -- reused per call, never allocated.</summary>
    private readonly List<Vector2> _tetherRouteScratch = [];

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
    ///     Wander/patrol variant of <see cref="TryFindPath" /> (behavior contract <c>A2-mesh-tether</c>, modeling
    ///     legacy <c>mWORLD.Path</c>'s clamp-to-last-valid-point behavior toward an off-mesh destination -- a
    ///     Fenrir-owned superset, not byte-parity: legacy marches in fixed <c>speed*dTime</c> steps and validates
    ///     every intermediate step; this bisects the straight XZ line for the mesh boundary in a fixed iteration
    ///     count instead, with no Fenrir-chosen world-unit step-size constant to tune. There is also no
    ///     "intermediate step lands off the navmesh" case to clamp against here at all, unlike legacy's raycast
    ///     march: A* only ever traverses connected walkable triangles, so a resolved route can never leave the
    ///     mesh mid-corridor -- only the requested destination itself can be off-mesh, which is exactly what this
    ///     clamps.
    /// </summary>
    /// <remarks>
    ///     When <paramref name="to" /> already resolves to a walkable triangle this is identical to
    ///     <see cref="TryFindPath" />. When it does not, the destination is clamped to the last point still on
    ///     the navmesh along the straight line from <paramref name="from" /> toward <paramref name="to" />
    ///     (<see cref="ClampToMeshBoundary" />) and the ordinary route is computed to that clamped point instead
    ///     of failing outright. If <paramref name="from" /> itself is off-mesh there is nothing to clamp toward
    ///     -- this fails exactly like <see cref="TryFindPath" /> would (matching the contract's "or the unmoved
    ///     start point if no intermediate step ever validated" fallback: with the start itself invalid, no
    ///     result is possible at all).
    /// </remarks>
    public bool TryFindPathClamped(Vector3 from, Vector3 to, List<Vector2> waypointsOut)
    {
        if (!_geometry.TryFindContainingWalkableTriangle(from.X, from.Z, out _))
        {
            waypointsOut.Clear();
            return false;
        }

        if (_geometry.TryFindContainingWalkableTriangle(to.X, to.Z, out _))
            return TryFindPath(from, to, waypointsOut);

        var clamped = ClampToMeshBoundary(from, to);
        return TryFindPath(from, clamped, waypointsOut);
    }

    /// <summary>
    ///     Bisects the straight XZ line from <paramref name="from" /> (already validated walkable by every
    ///     caller of <see cref="TryFindPathClamped" />) toward the off-mesh <paramref name="to" /> for the
    ///     boundary point where the navmesh's walkable footprint ends, using
    ///     <see cref="ZoneGeometry.TryFindContainingWalkableTriangle" /> (the same floor-only walkable test
    ///     <see cref="TryFindPath" /> itself resolves start/goal triangles with, not the wall-inclusive
    ///     <see cref="ZoneGeometry.IsWalkable" />) as the per-sample test. Always converges to a walkable point --
    ///     worst case, back to <paramref name="from" /> itself if no interior point along the line ever
    ///     validates.
    /// </summary>
    private Vector3 ClampToMeshBoundary(Vector3 from, Vector3 to)
    {
        var lowWalkableFraction = 0f; // `from` itself -- guaranteed walkable by the caller.
        var highBlockedFraction = 1f; // `to` itself -- guaranteed off-mesh by the caller.

        for (var i = 0; i < BoundaryBisectionIterations; i++)
        {
            var mid = (lowWalkableFraction + highBlockedFraction) * 0.5f;
            var x = from.X + (to.X - from.X) * mid;
            var z = from.Z + (to.Z - from.Z) * mid;

            if (_geometry.TryFindContainingWalkableTriangle(x, z, out _))
                lowWalkableFraction = mid;
            else
                highBlockedFraction = mid;
        }

        var clampedX = from.X + (to.X - from.X) * lowWalkableFraction;
        var clampedZ = from.Z + (to.Z - from.Z) * lowWalkableFraction;
        return new Vector3(clampedX, from.Y, clampedZ);
    }

    /// <summary>
    ///     Pursuit-tether variant of <see cref="TryFindPath" /> (behavior contract <c>A2-mesh-tether</c>,
    ///     modeling legacy <c>PathForMonsterAttack</c>'s "stop advancing the instant the candidate waypoint
    ///     would already sit within this distance of the anchor" affordance as a post-funnel clip rather than a
    ///     step-loop early-exit -- a Fenrir-owned superset, not byte-parity; only the presence/purpose of the
    ///     tether is legacy-cited, never its geometry). <paramref name="tetherAnchor" /> is the live position of
    ///     the specific avatar being chased, captured once by the caller at decision time -- never the monster's
    ///     own spawn point -- and <paramref name="tetherRadius" /> is normally the monster's own attack radius.
    /// </summary>
    /// <remarks>
    ///     Computes the ordinary route to <paramref name="to" /> and then truncates it at the first point along
    ///     that route (start included) that already sits within <paramref name="tetherRadius" /> of
    ///     <paramref name="tetherAnchor" />, clipping the crossing segment to the exact circle boundary instead
    ///     of snapping to whichever funnel corner first lands inside -- a more precise stop point than legacy's
    ///     fixed-step march produces, not a coarser one. Fails exactly when the underlying <see cref="TryFindPath" />
    ///     call fails (off-mesh endpoint or unreachable goal): unlike <see cref="TryFindPathClamped" />, there is
    ///     deliberately no partial-credit clamp here -- an unreachable pursuit destination reverts wholesale to
    ///     no route, matching the contract's documented divergence between the two decision-time legacy
    ///     functions ("the entire call reverts wholesale to the unmoved start point").  If
    ///     <paramref name="from" /> already sits within the tether radius, this reports success with
    ///     <paramref name="waypointsOut" /> left empty -- the degenerate "already close enough, commit to the
    ///     chase with zero displacement" case the contract documents for a destination reached on the very first
    ///     internal iteration -- without spending a full route computation just to discard nearly all of it.
    /// </remarks>
    public bool TryFindPursuitPath(Vector3 from, Vector3 to, Vector2 tetherAnchor, float tetherRadius,
        List<Vector2> waypointsOut)
    {
        waypointsOut.Clear();

        var fromXz = new Vector2(from.X, from.Z);
        var tetherRadiusSq = tetherRadius * tetherRadius;

        if (Vector2.DistanceSquared(fromXz, tetherAnchor) <= tetherRadiusSq)
            return true;

        if (!TryFindPath(from, to, _tetherRouteScratch))
            return false;

        ClipToTether(fromXz, tetherAnchor, tetherRadiusSq, waypointsOut);
        return true;
    }

    /// <summary>
    ///     Walks <see cref="_tetherRouteScratch" /> (the unclamped route just resolved by <see cref="TryFindPursuitPath" />)
    ///     from <paramref name="from" />, copying each point into <paramref name="waypointsOut" /> until one
    ///     lands within the tether circle -- at which point that final segment is clipped to the exact circle
    ///     boundary crossing (<see cref="ClipSegmentToCircle" />) and the walk stops. If no point along the
    ///     route ever enters tether range (the caller's chosen destination sits outside the tether radius
    ///     entirely -- e.g. a tether radius smaller than the caller's own attack-position offset), the full
    ///     unclamped route is copied through unchanged: still a valid, safe route, simply not an early stop.
    /// </summary>
    private void ClipToTether(Vector2 from, Vector2 anchor, float tetherRadiusSq, List<Vector2> waypointsOut)
    {
        var segmentStart = from;
        foreach (var point in _tetherRouteScratch)
        {
            if (Vector2.DistanceSquared(point, anchor) <= tetherRadiusSq)
            {
                waypointsOut.Add(ClipSegmentToCircle(segmentStart, point, anchor, tetherRadiusSq));
                return;
            }

            waypointsOut.Add(point);
            segmentStart = point;
        }
    }

    /// <summary>
    ///     Point where segment <paramref name="segmentStart" />-&gt;<paramref name="segmentEnd" /> first enters
    ///     the circle of squared radius <paramref name="radiusSq" /> around <paramref name="anchor" />, via the
    ///     standard line-circle quadratic. Every call site here guarantees <paramref name="segmentStart" /> sits
    ///     outside the circle and <paramref name="segmentEnd" /> sits inside or on it, so a real root in [0,1]
    ///     always exists; the degenerate/defensive branches (zero-length segment, a negative discriminant that
    ///     should not occur given that guarantee) fall back to <paramref name="segmentEnd" /> itself rather than
    ///     ever producing NaN.
    /// </summary>
    private static Vector2 ClipSegmentToCircle(Vector2 segmentStart, Vector2 segmentEnd, Vector2 anchor,
        float radiusSq)
    {
        var direction = segmentEnd - segmentStart;
        var a = Vector2.Dot(direction, direction);
        if (a <= 0f)
            return segmentEnd;

        var toAnchor = segmentStart - anchor;
        var b = 2f * Vector2.Dot(toAnchor, direction);
        var c = Vector2.Dot(toAnchor, toAnchor) - radiusSq;

        var discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
            return segmentEnd;

        var t = (-b - MathF.Sqrt(discriminant)) / (2f * a);
        t = Math.Clamp(t, 0f, 1f);
        return segmentStart + direction * t;
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
