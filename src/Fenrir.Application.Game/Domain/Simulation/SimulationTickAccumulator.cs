namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class SimulationTickAccumulator
{
    private TimeSpan _accumulated;

        public int Advance(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
            return 0;

        _accumulated += elapsed;

        var dueTicks = (int)(_accumulated.Ticks / SimulationClock.LegacyTick.Ticks);
        if (dueTicks <= 0)
            return 0;

        _accumulated -= SimulationClock.ToTimeSpan(dueTicks);
        return 1;
    }
}
