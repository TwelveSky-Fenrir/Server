using Fenrir.Application.Game.Mounts;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;

/// <summary>Business logic behind <see cref="MountAbsorbHandler" /> (CZ_ANIMAL_ABSORB_SEND, op113).</summary>
public interface IMountAbsorbService
{
    bool TryAbsorb(Zone zone, PlayerRuntimeState state, int characterId);

    void Release(Zone zone, PlayerRuntimeState state, int characterId);
}

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
