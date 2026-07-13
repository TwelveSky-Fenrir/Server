using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class FishingCatchHandler(IFishingCatchService fishingCatchService, ILogger<FishingCatchHandler> logger)
    : IAsyncPacketHandler<FishingCatchRequest>
{
    public async ValueTask HandleAsync(FishingCatchRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: FishingCatchRequest (op105) received for character {CharacterId}",
            session.SessionId, characterId);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != FishingLineHandler.FishingZoneNumber)
        {
            logger.LogWarning(
                "Fishing-catch request rejected for character {CharacterId}: map {MapId} is not the fishing zone -- aborting session",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
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
