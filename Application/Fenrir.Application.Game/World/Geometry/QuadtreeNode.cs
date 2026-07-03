using System.Numerics;

namespace Fenrir.Application.Game.World.Geometry;

/// <summary>
///     One node of a zone's collision quadtree. Internal nodes have all four <see cref="ChildNodeIndex" /> entries
///     set (no <c>-1</c> sentinel) and an empty <see cref="TriangleIndex" />; leaf nodes have <c>-1</c> children and
///     list the <see cref="WorldTriangle" /> indices (into <see cref="ZoneGeometry.Triangles" />) that fall inside
///     their box. The box test used during descent only ever compares X/Z (see <see cref="ZoneGeometry" />), so
///     <see cref="BoxMin" />/<see cref="BoxMax" /> carry a Y extent purely because the source format does; it is
///     never read.
/// </summary>
public readonly record struct QuadtreeNode(Vector3 BoxMin, Vector3 BoxMax, int[] TriangleIndex, int[] ChildNodeIndex)
{
    public bool IsLeaf => ChildNodeIndex[0] == -1;

    public bool ContainsXz(float x, float z)
    {
        return x >= BoxMin.X && x <= BoxMax.X && z >= BoxMin.Z && z <= BoxMax.Z;
    }
}
