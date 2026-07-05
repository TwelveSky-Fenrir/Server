namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     Bridges the 20 Hz network frame to the 2 Hz legacy simulation: each frame feeds its elapsed time into
///     <see cref="Advance" />, which returns how many whole 500 ms legacy ticks that time paid for. The
///     sub-tick remainder is carried over, never discarded.
/// </summary>
/// <remarks>Not thread-safe -- owned and called only by its Zone's tick loop.</remarks>
public sealed class SimulationTickAccumulator
{
    private TimeSpan _accumulated;

    /// <summary>Zero or negative elapsed time (a clock hiccup) contributes nothing rather than rewinding the accumulator.</summary>
    public int Advance(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
            return 0;

        _accumulated += elapsed;

        var dueTicks = (int)(_accumulated.Ticks / SimulationClock.LegacyTick.Ticks);
        if (dueTicks > 0)
            _accumulated -= SimulationClock.ToTimeSpan(dueTicks);

        return dueTicks;
    }
}
