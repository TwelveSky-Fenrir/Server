namespace Fenrir.Domain.Game.Primitives;

public readonly record struct WorldPosition(float X, float Y, float Z)
{

        public static WorldPosition Origin => default;

        public float DistanceSquaredXz(in WorldPosition other)
    {
        var dx = X - other.X;
        var dz = Z - other.Z;
        return (dx * dx) + (dz * dz);
    }

        public float HeightDelta(in WorldPosition other) => System.Math.Abs(Y - other.Y);
}
