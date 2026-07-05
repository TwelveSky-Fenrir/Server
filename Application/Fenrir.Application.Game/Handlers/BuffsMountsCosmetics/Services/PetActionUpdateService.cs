using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;

/// <summary>Business logic behind <see cref="PetActionUpdateHandler" /> (CZ_UPDATE_PET_ACTION_SEND, op156).</summary>
public interface IPetActionUpdateService
{
    void Apply(Zone zone, int characterId, in ActionInfo action);
}

public sealed class PetActionUpdateService : IPetActionUpdateService
{
    public void Apply(Zone zone, int characterId, in ActionInfo action)
    {
        zone.Post(ZoneCommand.PetAction(characterId, in action));
    }
}
