using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public ImmutableArray<int> MountActivity { get; set; } = ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public bool MountExpUp { get; set; }

    public int MountExpiryCountdownAccrualTicks { get; set; }

    public int MountActivityDecayAccrualTicks { get; set; }

    public bool MountAutoDismountPending { get; set; }
}
