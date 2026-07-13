using System.Numerics;

namespace Fenrir.Application.Game.Domain.World.Geometry;

public readonly record struct WorldTriangle(Vector3 Vertex0, Vector3 Vertex1, Vector3 Vertex2, Vector4 PlaneInfo);
