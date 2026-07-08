using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_GET_REWARD_ITEM_SEND (opcode 154) -- the 7-day login-reward catalog plus this character's claim
///     cursor.
/// </summary>
public sealed class GetDailyRewardCatalogHandler(
    IGetDailyRewardCatalogService service,
    ILogger<GetDailyRewardCatalogHandler> logger)
    : IAsyncPacketHandler<GetDailyRewardCatalogRequest>
{
    public async ValueTask HandleAsync(GetDailyRewardCatalogRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: GetDailyRewardCatalogRequest (op154) received for character {CharacterId}",
            session.SessionId, characterId);

        var response = await service.GetCatalogAsync(characterId, cancellationToken);

        session.Send(response);
    }
}
