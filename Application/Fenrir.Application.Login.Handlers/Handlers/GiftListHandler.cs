using Fenrir.Application.Login.Abstractions.GiftList;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op25 CL_GIFT_INFO_SEND — first 10 pending gifts (oldest-first), [i][0]=ProductId,[i][1]=0 matching legacy
///     uGiftInfo shape; 11th+ still claimable once the list shifts.
/// </summary>
public sealed class GiftListHandler(IGiftListService giftListService) : IAsyncPacketHandler<GiftListRequest>
{
    public async ValueTask HandleAsync(GiftListRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        var giftItem = await giftListService.GetGiftListAsync(accountId, cancellationToken);

        session.Send(new GiftListResponse { Result = 0, GiftItem = giftItem });
    }
}
