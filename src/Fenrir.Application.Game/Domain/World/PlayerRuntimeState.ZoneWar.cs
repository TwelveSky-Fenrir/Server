namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public int AntiCampingProximityCounter { get; set; }

    public int AfkTick { get; set; }
}
