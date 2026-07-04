using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op25 CL_GIFT_INFO_SEND — the 10-page gift list (login protocol report §4.25, "chantier V8"). Now
///     wired to the REAL delivery queue (game.Gifts, usp_Gift_GetPendingByAccount) instead of the static
///     all-zero placeholder a prior pass pinned for the pre-V8 EU33 population: up to the first 10 pending
///     (oldest-first, delivery order) gifts fill <c>[i][0]=ProductId,[i][1]=0</c>, matching the legacy's
///     own <c>[i][0]=uGiftInfo[i],[i][1]=0</c> shape exactly (V1/MAX_GIFT_ITEM_PAGE_VALUE_NUM=2 layout,
///     GIFT_V2 still off). A pending count beyond 10 is simply not shown here (same "only 10 pages exist"
///     ceiling the wire itself imposes) -- an 11th+ gift is still safely claimable later once an earlier
///     one is claimed and the list shifts.
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
