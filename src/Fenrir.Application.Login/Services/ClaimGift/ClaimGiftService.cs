using CaeriusNet.Exceptions;
using Fenrir.Application.Login.Abstractions.ClaimGift;
using Fenrir.Application.Login.Sessions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.ClaimGift;

public sealed class ClaimGiftService(IGiftRepository gifts, ILogger<ClaimGiftService> logger) : IClaimGiftService
{
    private const int SqlErrorGiftUnavailable = 50220;
    private const int SqlErrorVaultFull = 50274;

    public async ValueTask<ClaimGiftResult> ClaimGiftAsync(int accountId, int giftInfoIndex, GiftSlotBoard slots,
        CancellationToken cancellationToken)
    {
        if (!slots.IsLoaded)
            slots.Refresh(await gifts.GetPendingByAccountAsync(accountId, cancellationToken));

        if (!slots.TryTake(giftInfoIndex, out var entry))
        {
            logger.LogWarning("Gift claim rejected: account {AccountId} slot {GiftInfoIndex} is empty", accountId,
                giftInfoIndex);
            return new ClaimGiftResult(ClaimGiftOutcome.GiftUnavailable);
        }

        try
        {
            await gifts.ClaimIntoVaultAsync(entry.GiftId, accountId, cancellationToken);
            return new ClaimGiftResult(ClaimGiftOutcome.Success);
        }
        catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException { Number: SqlErrorGiftUnavailable })
        {
            logger.LogWarning(ex,
                "Account {AccountId} gift claim slot {GiftInfoIndex} (GiftId {GiftId}) lost a claim race",
                accountId, giftInfoIndex, entry.GiftId);
            return new ClaimGiftResult(ClaimGiftOutcome.GiftUnavailable);
        }
        catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException { Number: SqlErrorVaultFull })
        {
            slots.Restore(giftInfoIndex, entry);
            logger.LogWarning(ex,
                "Account {AccountId} gift claim slot {GiftInfoIndex} (GiftId {GiftId}) rejected: vault is full",
                accountId, giftInfoIndex, entry.GiftId);
            return new ClaimGiftResult(ClaimGiftOutcome.VaultFull);
        }
        catch (Exception ex)
        {
            slots.Restore(giftInfoIndex, entry);
            logger.LogWarning(ex,
                "Account {AccountId} gift claim slot {GiftInfoIndex} (GiftId {GiftId}) failed unexpectedly",
                accountId, giftInfoIndex, entry.GiftId);
            return new ClaimGiftResult(ClaimGiftOutcome.PersistenceFailure);
        }
    }
}
