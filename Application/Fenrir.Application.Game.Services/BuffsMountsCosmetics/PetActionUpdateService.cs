using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

public sealed class PetActionUpdateService : IPetActionUpdateService
{
    public void Apply(Zone zone, int characterId, in ActionInfo action)
    {
        zone.Post(ZoneCommand.PetAction(characterId, in action));
    }
}
