using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

// Legacy asks ts25extra per request (SELECT s_tchange{0,1,2} FROM skyinfo, Server/ts25extra/S08_MyDB.cpp:1220);
// with no extra process the same operator switch is game.WorldStateTribes.IsClosed, loaded once at boot.
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
