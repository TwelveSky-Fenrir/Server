namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class MonsterSymbolAttackWindowTracker
{
    private readonly Lock _lock = new();
    private int _elapsedLegacyTicksSinceCapture;
    private byte? _lastHolder;
    private bool _notified;

    public bool ShouldNotifyNow(byte holderTribe, int legacyTicksElapsed, int delayLegacyTicks)
    {
        lock (_lock)
        {
            if (_lastHolder != holderTribe)
            {
                _lastHolder = holderTribe;
                _elapsedLegacyTicksSinceCapture = 0;
                _notified = false;
            }

            _elapsedLegacyTicksSinceCapture += legacyTicksElapsed;

            if (_notified || _elapsedLegacyTicksSinceCapture < delayLegacyTicks)
                return false;

            _notified = true;
            return true;
        }
    }
}
