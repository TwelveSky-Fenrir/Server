using Fenrir.Application.Login.Abstractions.GiftList;
using Fenrir.Network.Abstractions;
using Fenrir.Application.Login.Packets;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class GiftListHandler(IGiftListService giftListService, ILogger<GiftListHandler> logger)
    : IAsyncPacketHandler<GiftListRequest>
{
    public async ValueTask HandleAsync(GiftListRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Session {SessionId}: op25 CL_GIFT_INFO_SEND received for account {AccountId}",
                session.SessionId, accountId);

        var giftItem = await giftListService.GetGiftListAsync(accountId, cancellationToken);

        session.Send(new GiftListResponse { Result = 0, GiftItem = giftItem });
    }
}
