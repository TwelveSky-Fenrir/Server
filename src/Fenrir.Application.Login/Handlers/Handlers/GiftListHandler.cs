using Fenrir.Application.Login.Abstractions.GiftList;
using Fenrir.Application.Login.Sessions;
using Fenrir.Protocol.Login;
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

        var giftItem = await giftListService.GetGiftListAsync(accountId, loginSession.GiftSlots, cancellationToken);

        session.Send(new GiftListResponse { Result = 0, GiftItem = giftItem });
    }
}
