using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

public sealed class MountAbsorbService : IMountAbsorbService
{
    public bool TryAbsorb(Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (!MountStateResolver.TryResolveActiveMountedMount(state.AnimalIndex, state.AnimalNumber,
                state.MountGarage, out _) || state.AnimalTime < 1 || state.AnimalAbsorbTime < 1)
            return false;

        return zone.PostMountCommand(new MountZoneCommand(characterId, AnimalAbsorbState: 1,
            Broadcast: MountBroadcastKind.AbsorbToggle));
    }

    public void Release(Zone zone, PlayerRuntimeState state, int characterId)
    {
        zone.PostMountCommand(new MountZoneCommand(characterId, AnimalAbsorbState: 0,
            Broadcast: MountBroadcastKind.AbsorbToggle));
    }
}
