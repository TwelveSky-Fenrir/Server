using System.Numerics;

namespace Fenrir.Application.Game.World.Geometry;

/// <summary>
///     Internal nodes have all four <see cref="ChildNodeIndex" /> set (no <c>-1</c>) and empty
///     <see cref="TriangleIndex" />; leaves have <c>-1</c> children and list their triangle indices.
///     <see cref="BoxMin" />/<see cref="BoxMax" /> carry a Y extent but it is never read (descent is X/Z-only).
/// </summary>
public readonly record struct QuadtreeNode(Vector3 BoxMin, Vector3 BoxMax, int[] TriangleIndex, int[] ChildNodeIndex)
{
    public bool IsLeaf => ChildNodeIndex[0] == -1;

    public bool ContainsXz(float x, float z)
    {
        return x >= BoxMin.X && x <= BoxMax.X && z >= BoxMin.Z && z <= BoxMax.Z;
    }
}
