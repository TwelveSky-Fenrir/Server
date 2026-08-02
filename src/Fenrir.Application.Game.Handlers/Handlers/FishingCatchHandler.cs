using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class FishingCatchHandler(IFishingCatchService fishingCatchService, ILogger<FishingCatchHandler> logger)
    : IAsyncPacketHandler<FishingCatchRequest>
{
    public async ValueTask HandleAsync(FishingCatchRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: FishingCatchRequest (op105) received for character {CharacterId}",
            session.SessionId, characterId);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != FishingLineHandler.FishingZoneNumber)
        {
            logger.LogDebug(
                "Fishing-catch request ignored for character {CharacterId}: map {MapId} is not the fishing zone",
                characterId, zone.MapId);
            return;
        }

        if (state.FishingState == 0 || !state.CatchingFish)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await fishingCatchService.ResolveAndApplyAsync(zone, state, characterId, session, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
