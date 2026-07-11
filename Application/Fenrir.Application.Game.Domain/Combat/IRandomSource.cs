namespace Fenrir.Application.Game.Domain.Combat;

public interface IRandomSource
{
    public int NextInt32(int exclusiveUpperBound);
}

public sealed class SystemRandomSource : IRandomSource
{
    public static readonly SystemRandomSource Instance = new();

    public int NextInt32(int exclusiveUpperBound)
    {
        return Random.Shared.Next(exclusiveUpperBound);
    }
}
