using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

/// <summary>Business logic behind <see cref="PetActionUpdateHandler" /> (CZ_UPDATE_PET_ACTION_SEND, op156).</summary>
public interface IPetActionUpdateService
{
    public void Apply(Zone zone, int characterId, in ActionInfo action);
}
