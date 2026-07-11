namespace Fenrir.Application.Game.Domain.Gm;

public sealed class YangGokPvpDropEventState
{

        public const int EnabledDropRatePercent = 35;

    private volatile int _dropRatePercent;

    private volatile bool _enabled;

        public bool Enabled => _enabled;

        public int DropRatePercent => _dropRatePercent;

        public void Enable()
    {
        _dropRatePercent = EnabledDropRatePercent;
        _enabled = true;
    }

        public void Disable()
    {
        _enabled = false;
    }
}
