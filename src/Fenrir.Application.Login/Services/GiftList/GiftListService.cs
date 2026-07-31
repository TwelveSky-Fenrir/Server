using Fenrir.Application.Login.Abstractions.GiftList;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.GiftList;

public sealed class GiftListService(IGiftRepository gifts, ILogger<GiftListService> logger) : IGiftListService
{
    private const int MaxGiftPages = 10;

    public async ValueTask<int[]> GetGiftListAsync(int accountId, CancellationToken cancellationToken)
    {
        var pending = await gifts.GetPendingByAccountAsync(accountId, cancellationToken);

        var slots = pending.OrderBy(g => g.CreatedAtUtc).ThenBy(g => g.GiftId).ToArray();

        var giftItem = new int[MaxGiftPages * 2];
        for (var i = 0; i < slots.Length && i < MaxGiftPages; i++)
            giftItem[i * 2] = slots[i].ProductId ?? 0;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Gift list read for account {AccountId}: {PendingCount} pending ({ShownCount} shown)",
                accountId, pending.Count, Math.Min(pending.Count, MaxGiftPages));

        return giftItem;
    }
}
