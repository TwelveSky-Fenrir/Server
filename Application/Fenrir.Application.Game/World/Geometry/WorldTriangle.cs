using System.Numerics;

namespace Fenrir.Application.Game.World.Geometry;

/// <summary>
///     Collision triangle from a legacy <c>.WM</c> file. The source record also carries a texture index,
///     normals, UVs and a bounding sphere -- rendering-only fields dropped here as unneeded.
/// </summary>
/// <param name="PlaneInfo">Plane coefficients (A, B, C, D) of A*x + B*y + C*z = D, precomputed by the content pipeline.</param>
public readonly record struct WorldTriangle(Vector3 Vertex0, Vector3 Vertex1, Vector3 Vertex2, Vector4 PlaneInfo);
