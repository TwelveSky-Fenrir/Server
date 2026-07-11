namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class Zone335StartTrigger
{
    private readonly Lock _lock = new();
    private int _remainingTicks;
    private bool _startRequested;

        public int RemainingTicks
    {
        get
        {
            lock (_lock)
            {
                return _remainingTicks;
            }
        }
    }

        public bool StartRequested
    {
        get
        {
            lock (_lock)
            {
                return _startRequested;
            }
        }
    }

        public void Request(int countdownTicks)
    {
        lock (_lock)
        {
            _remainingTicks = countdownTicks;
            _startRequested = true;
        }
    }

        public bool ConsumeStartRequest()
    {
        lock (_lock)
        {
            if (!_startRequested)
                return false;

            _startRequested = false;
            _remainingTicks = 0;
            return true;
        }
    }
}
