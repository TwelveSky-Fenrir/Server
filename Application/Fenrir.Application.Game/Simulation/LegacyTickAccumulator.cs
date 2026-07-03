namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     Bridges the 20 Hz network frame to the 2 Hz legacy simulation (plan decision D4): each frame feeds its
///     real elapsed time into <see cref="Advance" />, which returns how many WHOLE 500 ms legacy ticks
///     (<see cref="LegacyTime.LegacyTick" />) that time paid for — usually 0, one frame in ten returning 1.
///     The sub-tick remainder is carried over, never discarded, so the long-run legacy tick rate never drifts
///     from wall-clock time no matter how irregular the frame cadence is.
/// </summary>
/// <remarks>
///     Not thread-safe by design — owned and called only by its <see cref="World.Zone" />'s tick loop, like
///     every other piece of zone state (single-writer invariant). A stalled host (GC pause, debugger) delivers
///     the missed ticks as one burst on the next frame: that is the correct catch-up semantic for legacy timers
///     counted in ticks (buffs, respawn), which must not silently lose simulated time.
/// </remarks>
public sealed class LegacyTickAccumulator
{
    private TimeSpan _accumulated;

    /// <summary>
    ///     Adds <paramref name="elapsed" /> real time and returns the number of whole legacy ticks now due.
    ///     Zero or negative elapsed time (a clock hiccup) contributes nothing rather than rewinding the
    ///     accumulator.
    /// </summary>
    public int Advance(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
            return 0;

        _accumulated += elapsed;

        var dueTicks = (int)(_accumulated.Ticks / LegacyTime.LegacyTick.Ticks);
        if (dueTicks > 0)
            _accumulated -= LegacyTime.ToTimeSpan(dueTicks);

        return dueTicks;
    }
}
