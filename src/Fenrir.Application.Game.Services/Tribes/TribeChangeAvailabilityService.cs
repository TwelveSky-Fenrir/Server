using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

public sealed class TribeChangeAvailabilityService(
    WorldStateService worldState,
    ILogger<TribeChangeAvailabilityService> logger) : ITribeChangeAvailabilityService
{
    public bool IsChangeToTribeAllowed(byte toTribe)
    {
        if (!TribeConversionResolver.IsPlayableTribe(toTribe))
            return false;

        var closed = worldState.GetTribe(toTribe).IsClosed;
        if (closed)
            logger.LogDebug("Faction transfer to tribe {ToTribe} refused: destination tribe is closed", toTribe);

        return !closed;
    }
}
