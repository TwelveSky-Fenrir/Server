using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

public interface IMountAbsorbService
{
    public bool TryAbsorb(Zone zone, PlayerRuntimeState state, int characterId);

    public void Release(Zone zone, PlayerRuntimeState state, int characterId);
}
