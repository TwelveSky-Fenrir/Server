using Fenrir.Application.Login.Abstractions.GiftList;
using Fenrir.Application.Login.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.GiftList;

public sealed class GiftListService(IGiftRepository gifts, ILogger<GiftListService> logger) : IGiftListService
{
    public async ValueTask<int[]> GetGiftListAsync(int accountId, GiftSlotBoard slots,
        CancellationToken cancellationToken)
    {
        var pending = await gifts.GetPendingByAccountAsync(accountId, cancellationToken);

        slots.Refresh(pending);

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Gift list read for account {AccountId}: {PendingCount} pending, {OccupiedCount} slot(s) held",
                accountId, pending.Count, slots.OccupiedCount());

        return slots.ToWireArray();
    }
}
