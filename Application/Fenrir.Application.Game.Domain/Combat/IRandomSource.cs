namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Abstraction over <c>rand_mir()</c> -- one draw per call site, in legacy order, so tests can pin exact
///     sequences without reproducing the legacy PRNG.
/// </summary>
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
