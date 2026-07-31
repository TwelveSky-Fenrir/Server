using Fenrir.Application.Login.Abstractions.ClaimGift;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.ClaimGift;

public sealed class ClaimGiftService(IGiftRepository gifts, ILogger<ClaimGiftService> logger) : IClaimGiftService
{
    private const int SqlErrorGiftUnavailable = 50220;
    private const int SqlErrorVaultFull = 50274;

    public async ValueTask<ClaimGiftResult> ClaimGiftAsync(int accountId, int giftInfoIndex,
        CancellationToken cancellationToken)
    {
        var pending = await gifts.GetPendingByAccountAsync(accountId, cancellationToken);

        var slots = pending.OrderBy(g => g.CreatedAtUtc).ThenBy(g => g.GiftId).ToArray();

        if (giftInfoIndex < 0 || giftInfoIndex >= slots.Length)
            return new ClaimGiftResult(ClaimGiftOutcome.GiftUnavailable);

        var giftId = slots[giftInfoIndex].GiftId;

        try
        {
            await gifts.ClaimIntoVaultAsync(giftId, accountId, cancellationToken);
            return new ClaimGiftResult(ClaimGiftOutcome.Success);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorGiftUnavailable)
        {
            logger.LogWarning(ex,
                "Account {AccountId} gift claim index {GiftInfoIndex} (GiftId {GiftId}) lost a claim race",
                accountId, giftInfoIndex, giftId);
            return new ClaimGiftResult(ClaimGiftOutcome.GiftUnavailable);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorVaultFull)
        {
            logger.LogWarning(ex,
                "Account {AccountId} gift claim index {GiftInfoIndex} (GiftId {GiftId}) rejected: vault is full",
                accountId, giftInfoIndex, giftId);
            return new ClaimGiftResult(ClaimGiftOutcome.VaultFull);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Account {AccountId} gift claim index {GiftInfoIndex} (GiftId {GiftId}) failed unexpectedly",
                accountId, giftInfoIndex, giftId);
            return new ClaimGiftResult(ClaimGiftOutcome.PersistenceFailure);
        }
    }
}
