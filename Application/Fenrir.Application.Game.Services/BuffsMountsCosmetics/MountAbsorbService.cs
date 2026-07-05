using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

/// <inheritdoc cref="IMountAbsorbService" />
public sealed class MountAbsorbService : IMountAbsorbService
{
    public bool TryAbsorb(Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (state.AnimalIndex < MountStateResolver.SlotCount ||
            state.AnimalIndex > MountStateResolver.MountedMax || state.AnimalAbsorbTime < 1)
            return false;

        zone.PostMountCommand(new MountZoneCommand(characterId, AnimalAbsorbState: 1,
            Broadcast: MountBroadcastKind.AbsorbToggle));
        return true;
    }

    public void Release(Zone zone, PlayerRuntimeState state, int characterId)
    {
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
        var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
        zone.PostMountCommand(new MountZoneCommand(characterId, AnimalAbsorbState: 0, Life: maxLife,
            Mana: maxMana, Broadcast: MountBroadcastKind.AbsorbToggle));
    }
}
