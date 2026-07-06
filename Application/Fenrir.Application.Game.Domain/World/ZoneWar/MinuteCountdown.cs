namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Shared "N simulated minutes have elapsed" accumulator for the Zone037/038 scheduled state machines.
///     Both translate the legacy step-counter cadence (<c>GetGameTickMinute</c>/<c>GetGameTickHour</c>,
///     Server/Header/function.h:1642-1652 -- a hardcoded 120-legacy-ticks-per-minute assumption) into real
///     elapsed time: since every legacy tick is ~500 ms in the shipped configuration, 120 ticks is exactly
///     60 s, so "one simulated minute" here is simply one real <see cref="TimeSpan" /> minute of accumulated
///     <see cref="Advance" /> calls -- no separate tick-count field is modeled. A caller that stalls for
///     several minutes' worth of real time before its next <see cref="Advance" /> call still catches up
///     correctly in that one call (returns &gt;1), matching every other burst-tolerant system in this cluster
///     (e.g. <see cref="Simulation.StunCountdownSystem" />).
/// </summary>
public sealed class MinuteCountdown
{
    private TimeSpan _accumulated;

    /// <summary>Total whole simulated minutes elapsed since the last <see cref="Reset" />.</summary>
    public int MinutesElapsed { get; private set; }

    public void Reset()
    {
        _accumulated = TimeSpan.Zero;
        MinutesElapsed = 0;
    }

    /// <summary>
    ///     Advances by real time <paramref name="elapsed" />; returns how many whole minutes just ticked over (usually 0
    ///     or 1).
    /// </summary>
    public int Advance(TimeSpan elapsed)
    {
        _accumulated += elapsed;
        var wholeMinutes = 0;

        while (_accumulated >= TimeSpan.FromMinutes(1))
        {
            _accumulated -= TimeSpan.FromMinutes(1);
            MinutesElapsed++;
            wholeMinutes++;
        }

        return wholeMinutes;
    }
}
