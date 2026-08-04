namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class MinuteCountdown
{
    private TimeSpan _accumulated;

    public int MinutesElapsed { get; private set; }

    public void Reset()
    {
        _accumulated = TimeSpan.Zero;
        MinutesElapsed = 0;
    }

    public int Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));

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

    public MinuteCountdownSnapshot Snapshot()
    {
        return new MinuteCountdownSnapshot(_accumulated.Ticks, MinutesElapsed);
    }

    public void Restore(in MinuteCountdownSnapshot snapshot)
    {
        if (!snapshot.HasExpectedShape)
            throw new ArgumentException("The minute countdown snapshot is invalid.", nameof(snapshot));

        _accumulated = TimeSpan.FromTicks(snapshot.AccumulatedTicks);
        MinutesElapsed = snapshot.MinutesElapsed;
    }
}

public readonly record struct MinuteCountdownSnapshot(long AccumulatedTicks, int MinutesElapsed)
{
    public bool HasExpectedShape =>
        AccumulatedTicks is >= 0 and < TimeSpan.TicksPerMinute && MinutesElapsed >= 0;
}
