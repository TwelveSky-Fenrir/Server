using Fenrir.Data.Accounts;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Services;

public enum ClaimGiftOutcome
{
    IndexNotPending,
    ClaimFailed,
    Success
}

public readonly record struct ClaimGiftResult(ClaimGiftOutcome Outcome);

public interface IClaimGiftService
{
    ValueTask<ClaimGiftResult> ClaimGiftAsync(int accountId, int giftInfoIndex, CancellationToken cancellationToken);
}

/// <summary>
///     op21 CL_WANT_GIFT_SEND business logic — GiftInfoIndex is a POSITION in the oldest-first pending list, not a
///     database key.
/// </summary>
public sealed class ClaimGiftService(IGiftRepository gifts, ILogger<ClaimGiftService> logger) : IClaimGiftService
{
    public async ValueTask<ClaimGiftResult> ClaimGiftAsync(int accountId, int giftInfoIndex,
        CancellationToken cancellationToken)
    {
        var pending = await gifts.GetPendingByAccountAsync(accountId, cancellationToken);
        if (giftInfoIndex >= pending.Count)
            return new ClaimGiftResult(ClaimGiftOutcome.IndexNotPending);

        try
        {
            await gifts.ClaimIntoVaultAsync(pending[giftInfoIndex].GiftId, accountId, cancellationToken);
        }
        catch (Exception ex)
        {
            // Broad catch by design: this project has no SqlClient dependency, so SQL error codes aren't inspected.
            logger.LogWarning(ex,
                "Account {AccountId} gift claim ClaimIntoVaultAsync failed (treated as vault full)", accountId);
            return new ClaimGiftResult(ClaimGiftOutcome.ClaimFailed);
        }

        return new ClaimGiftResult(ClaimGiftOutcome.Success);
    }
}
