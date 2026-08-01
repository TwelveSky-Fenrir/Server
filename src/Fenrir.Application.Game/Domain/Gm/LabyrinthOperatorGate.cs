namespace Fenrir.Application.Game.Domain.Gm;

public sealed class LabyrinthOperatorGate
{
    private volatile bool _enabled;

    public bool Enabled => _enabled;

    public void Enable()
    {
        _enabled = true;
    }

    public void Disable()
    {
        _enabled = false;
    }
}
