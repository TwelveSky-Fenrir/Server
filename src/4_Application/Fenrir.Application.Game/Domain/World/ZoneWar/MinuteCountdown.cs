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
