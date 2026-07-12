using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

public sealed class PetActionUpdateService(ILogger<PetActionUpdateService> logger) : IPetActionUpdateService
{
    public void Apply(Zone zone, int characterId, in ActionInfo action)
    {
        if (!zone.Post(ZoneCommand.PetAction(characterId, in action)))
            logger.LogWarning(
                "Zone {MapId} inbox full: dropped PetAction for character {CharacterId}",
                zone.MapId, characterId);
    }
}
