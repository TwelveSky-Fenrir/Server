using Fenrir.Application.Login.Abstractions.GiftList;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.GiftList;

/// <summary>
///     op25 CL_GIFT_INFO_SEND business logic — first 10 pending gifts (oldest-first), [i][0]=ProductId,[i][1]=0
///     matching legacy uGiftInfo shape; 11th+ still claimable once the list shifts.
/// </summary>
public sealed class GiftListService(IGiftRepository gifts, ILogger<GiftListService> logger) : IGiftListService
{
    private const int MaxGiftPages = 10;

    public async ValueTask<int[]> GetGiftListAsync(int accountId, CancellationToken cancellationToken)
    {
        var pending = await gifts.GetPendingByAccountAsync(accountId, cancellationToken);

        var giftItem = new int[MaxGiftPages * 2];
        for (var i = 0; i < pending.Count && i < MaxGiftPages; i++)
            giftItem[i * 2] = pending[i].ProductId ?? 0;

        // Routine per-request chatter (fires once per CL_GIFT_INFO_SEND), gated the same way SessionLoop
        // gates its own per-frame Debug logging -- pending.Count is already computed above regardless, so
        // the only avoided cost here is the string-formatting/boxing of the LogDebug call itself.
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Gift list read for account {AccountId}: {PendingCount} pending ({ShownCount} shown)",
                accountId, pending.Count, Math.Min(pending.Count, MaxGiftPages));

        return giftItem;
    }
}
