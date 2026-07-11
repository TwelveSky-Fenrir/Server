using System.Numerics;

namespace Fenrir.Application.Game.Domain.World.Geometry;

public readonly record struct QuadtreeNode(Vector3 BoxMin, Vector3 BoxMax, int[] TriangleIndex, int[] ChildNodeIndex)
{
    public bool IsLeaf => ChildNodeIndex[0] == -1;

    public bool ContainsXz(float x, float z)
    {
        return x >= BoxMin.X && x <= BoxMax.X && z >= BoxMin.Z && z <= BoxMax.Z;
    }
}
