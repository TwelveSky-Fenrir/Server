using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class GetDailyRewardCatalogHandler(
    IGetDailyRewardCatalogService service,
    ILogger<GetDailyRewardCatalogHandler> logger)
    : IAsyncPacketHandler<GetDailyRewardCatalogRequest>
{
    public async ValueTask HandleAsync(GetDailyRewardCatalogRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "Session {SessionId}: GetDailyRewardCatalogRequest (op154) received for account {AccountId}, character {CharacterId}",
            session.SessionId, accountId, zoneSession.CharacterId);

        var response = await service.GetCatalogAsync(accountId, cancellationToken);

        session.Send(response);
    }
}
