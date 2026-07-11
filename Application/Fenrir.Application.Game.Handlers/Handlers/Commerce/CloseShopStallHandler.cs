using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class CloseShopStallHandler(ICloseShopStallService service, ILogger<CloseShopStallHandler> logger)
    : IAsyncPacketHandler<CloseShopStallRequest>
{
    public async ValueTask HandleAsync(CloseShopStallRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug("CloseShopStall: session {SessionId} character {CharacterId} sort {Sort}",
            session.SessionId, characterId, packet.Sort);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            logger.LogWarning(
                "Close shop stall rejected: character {CharacterId} is outside the market district (zone {MapId}) -- session will be disconnected",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        switch (packet.Sort)
        {
            case 1:
                var response = service.CloseLiveShop(state);
                if (response is { } r)
                    session.Send(r);
                break;
            case 2:
                await service.CloseOfflineShopAsync(characterId, zone, cancellationToken);
                break;
        }
    }
}
