using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

/// <summary>Business logic behind <see cref="MountAbsorbHandler" /> (CZ_ANIMAL_ABSORB_SEND, op113).</summary>
public interface IMountAbsorbService
{
    public bool TryAbsorb(Zone zone, PlayerRuntimeState state, int characterId);

    public void Release(Zone zone, PlayerRuntimeState state, int characterId);
}
