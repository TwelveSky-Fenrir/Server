using System.Numerics;

namespace Fenrir.Application.Game.World.Geometry;

/// <summary>
///     One collision/ground triangle from a legacy <c>.WM</c> zone file. The source record (<c>WORLDTRIS_FOR_GXD</c>)
///     also carries a texture index, per-vertex normals and UV pairs, and a bounding sphere -- all client-rendering
///     fields never touched by the height/walkability queries this type exists to serve (<see cref="ZoneGeometry" />),
///     so they are intentionally not kept here; only vertex positions and the precomputed plane equation survive.
/// </summary>
/// <param name="PlaneInfo">
///     Plane equation coefficients (A, B, C, D) of A*x + B*y + C*z = D, precomputed by the
///     original content pipeline -- <see cref="ZoneGeometry" /> solves this for Y rather than recomputing a cross
///     product per query.
/// </param>
public readonly record struct WorldTriangle(Vector3 Vertex0, Vector3 Vertex1, Vector3 Vertex2, Vector4 PlaneInfo);
