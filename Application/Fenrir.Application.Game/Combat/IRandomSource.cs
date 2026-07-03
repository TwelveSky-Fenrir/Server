namespace Fenrir.Application.Game.Combat;

/// <summary>
///     Abstraction over the legacy's <c>rand_mir()</c> (report 05 §13 point 3: "la distribution exacte importe
///     peu, mais les seuils... supposent une plage précise (0-9999) à vérifier"). Every combat roll in this
///     namespace consumes exactly ONE draw per legacy <c>rand_mir() % N</c> call site, in the SAME order the
///     C++ makes them, so a test can pin an exact sequence of draws against a known outcome without needing to
///     reproduce the legacy PRNG bit-for-bit.
/// </summary>
public interface IRandomSource
{
    /// <summary>A non-negative integer in <c>[0, exclusiveUpperBound)</c> -- the C# analog of <c>rand_mir() % N</c>.</summary>
    int NextInt32(int exclusiveUpperBound);
}

/// <summary>Production default: <see cref="System.Random.Shared" />, safe to call from any thread.</summary>
public sealed class SystemRandomSource : IRandomSource
{
    public static readonly SystemRandomSource Instance = new();

    public int NextInt32(int exclusiveUpperBound)
    {
        return Random.Shared.Next(exclusiveUpperBound);
    }
}
