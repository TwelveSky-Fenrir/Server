namespace Fenrir.Domain.Game.Primitives;

/// <summary>
/// Position monde (little-endian <c>float</c>, comme <c>aLocation[3]</c> legacy). La distance de portée/AoI se
/// mesure en <b>XZ au carré</b> avec un contrôle de hauteur <b>séparé</b> (fidélité <c>ts25zone</c> : un port
/// euclidien 3D naïf changerait qui est « à portée » — cf. <c>Roadmap/CONNAISSANCE_LEGACY/02</c>, IA/AoI).
/// </summary>
public readonly record struct WorldPosition(float X, float Y, float Z)
{
    /// <summary>Origine (0,0,0).</summary>
    public static WorldPosition Origin => default;

    /// <summary>Distance horizontale au carré (plan XZ) — évite une racine carrée sur le hot path.</summary>
    public float DistanceSquaredXz(in WorldPosition other)
    {
        var dx = X - other.X;
        var dz = Z - other.Z;
        return (dx * dx) + (dz * dz);
    }

    /// <summary>Écart de hauteur absolu (axe Y), comparé séparément à un seuil de taille.</summary>
    public float HeightDelta(in WorldPosition other) => System.Math.Abs(Y - other.Y);
}
