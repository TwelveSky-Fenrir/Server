using System.Numerics;
using Fenrir.Application.Game.Domain.World.Geometry;

namespace Fenrir.Application.Game.Domain.World.Pathfinding;

public sealed class MonsterPathfinder
{
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

    public void ResetBudget()
    {
        _budgetRemaining = _perTickBudget;
    }

    public bool TryConsumeBudget()
    {
        if (_budgetRemaining <= 0)
            return false;

        _budgetRemaining--;
        return true;
    }

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

    private Vector3 ClampToMeshBoundary(Vector3 from, Vector3 to)
    {
        var lowWalkableFraction = 0f;
        var highBlockedFraction = 1f;

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

            if (TriArea2(apex, right, candidateRight) <= 0f)
            {
                if (apex == right || TriArea2(apex, left, candidateRight) > 0f)
                {
                    right = candidateRight;
                    rightIndex = i;
                }
                else
                {
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

            if (TriArea2(apex, left, candidateLeft) >= 0f)
            {
                if (apex == left || TriArea2(apex, right, candidateLeft) < 0f)
                {
                    left = candidateLeft;
                    leftIndex = i;
                }
                else
                {
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

        _portalLeft.Add(from);
        _portalRight.Add(from);

        for (var i = 0; i < _pathTriangles.Count - 1; i++)
        {
            var current = _pathTriangles[i];
            var next = _pathTriangles[i + 1];
            _graph.TryGetPortal(current, next, out var edgeA, out var edgeB);

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

        _portalLeft.Add(to);
        _portalRight.Add(to);
    }

    private static float TriArea2(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
    }
}
