using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op25 CL_GIFT_INFO_SEND — first 10 pending gifts (oldest-first), [i][0]=ProductId,[i][1]=0 matching legacy
///     uGiftInfo shape; 11th+ still claimable once the list shifts.
/// </summary>
public sealed class GiftListHandler(IGiftRepository gifts) : IAsyncPacketHandler<GiftListRequest>
{
    private const int MaxGiftPages = 10;

    public async ValueTask HandleAsync(GiftListRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        var pending = await gifts.GetPendingByAccountAsync(accountId, cancellationToken);

        var giftItem = new int[MaxGiftPages * 2];
        for (var i = 0; i < pending.Count && i < MaxGiftPages; i++)
            giftItem[i * 2] = pending[i].ProductId ?? 0;

        session.Send(new GiftListResponse { Result = 0, GiftItem = giftItem });
    }
}
