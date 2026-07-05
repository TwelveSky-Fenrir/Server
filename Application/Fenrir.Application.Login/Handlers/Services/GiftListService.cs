using Fenrir.Data.Accounts;

namespace Fenrir.Application.Login.Handlers.Services;

public interface IGiftListService
{
    ValueTask<int[]> GetGiftListAsync(int accountId, CancellationToken cancellationToken);
}

/// <summary>
///     op25 CL_GIFT_INFO_SEND business logic — first 10 pending gifts (oldest-first), [i][0]=ProductId,[i][1]=0
///     matching legacy uGiftInfo shape; 11th+ still claimable once the list shifts.
/// </summary>
public sealed class GiftListService(IGiftRepository gifts) : IGiftListService
{
    private const int MaxGiftPages = 10;

    public async ValueTask<int[]> GetGiftListAsync(int accountId, CancellationToken cancellationToken)
    {
        var pending = await gifts.GetPendingByAccountAsync(accountId, cancellationToken);

        var giftItem = new int[MaxGiftPages * 2];
        for (var i = 0; i < pending.Count && i < MaxGiftPages; i++)
            giftItem[i * 2] = pending[i].ProductId ?? 0;

        return giftItem;
    }
}
