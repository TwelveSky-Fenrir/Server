using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Progression;

public sealed class TowerUpgradeHandler(ITowerUpgradeService towerUpgradeService, ILogger<TowerUpgradeHandler> logger)
    : IAsyncPacketHandler<TowerUpgradeRequest>
{
    public async ValueTask HandleAsync(TowerUpgradeRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: TowerUpgradeRequest (op120) received for character {CharacterId}, index {Index}",
            session.SessionId, characterId, packet.Index);

        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await towerUpgradeService.UpgradeAsync(characterId, zone, state, packet, cancellationToken);

            if (result.Outcome != TowerUpgradeOutcome.Success)
            {
                logger.LogWarning(
                    "Tower-upgrade rejected for character {CharacterId} on map {MapId} -- aborting session",
                    characterId, zone.MapId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new TowerUpgradeResponse
            {
                Result = 0, Page = [result.PackedPage, 0], Index = [result.PackedIndex, 0], Count = 1
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
