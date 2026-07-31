namespace Fenrir.Application.Game.Domain.Forge;

public sealed class WarlordPityLockState
{
    public const int ForcedDrawValue = 50;

    public bool Engaged { get; private set; }

    public void Engage()
    {
        Engaged = true;
    }
}
