using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_END_PSHOP_SEND (opcode 32). <c>Sort</c> 1 closes the live personal shop, replying only if one was
///     actually open. <c>Sort</c> 2 closes the offline/deputy shop (ShopState only, items/money stay
///     attached) and sends no unicast reply, matching the legacy.
/// </summary>
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
