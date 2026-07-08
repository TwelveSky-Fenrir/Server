using Fenrir.Application.Login.Abstractions.ClaimGift;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.ClaimGift;

/// <summary>
///     op21 CL_WANT_GIFT_SEND business logic — GiftInfoIndex is a POSITION in the oldest-first pending list, not a
///     database key.
/// </summary>
public sealed class ClaimGiftService(IGiftRepository gifts, ILogger<ClaimGiftService> logger) : IClaimGiftService
{
    // usp_Gift_ClaimIntoVault.sql's two THROWn business-failure codes -- see the stored procedure and
    // ClaimGiftOutcome's own remarks for what each means to the caller.
    private const int SqlErrorGiftUnavailable = 50220;
    private const int SqlErrorVaultFull = 50274;

    public async ValueTask<ClaimGiftResult> ClaimGiftAsync(int accountId, int giftInfoIndex,
        CancellationToken cancellationToken)
    {
        var pending = await gifts.GetPendingByAccountAsync(accountId, cancellationToken);
        if (giftInfoIndex >= pending.Count)
            return new ClaimGiftResult(ClaimGiftOutcome.GiftUnavailable);

        try
        {
            await gifts.ClaimIntoVaultAsync(pending[giftInfoIndex].GiftId, accountId, cancellationToken);
            return new ClaimGiftResult(ClaimGiftOutcome.Success);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorGiftUnavailable)
        {
            // TOCTOU race: the gift was pending when GetPendingByAccountAsync read it above, but another
            // request (a replay on a second connection, or a racing claim of the same index) already flipped
            // its status by the time ClaimIntoVaultAsync ran. Same outward signal as a stale list index --
            // never "vault full", that would send the player to clear vault space that was never the issue.
            logger.LogWarning(ex,
                "Account {AccountId} gift claim index {GiftInfoIndex} (GiftId {GiftId}) lost a claim race",
                accountId, giftInfoIndex, pending[giftInfoIndex].GiftId);
            return new ClaimGiftResult(ClaimGiftOutcome.GiftUnavailable);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorVaultFull)
        {
            logger.LogWarning(ex,
                "Account {AccountId} gift claim index {GiftInfoIndex} (GiftId {GiftId}) rejected: vault is full",
                accountId, giftInfoIndex, pending[giftInfoIndex].GiftId);
            return new ClaimGiftResult(ClaimGiftOutcome.VaultFull);
        }
        catch (Exception ex)
        {
            // Anything else (a dropped connection, a command timeout, an unrelated SqlException) is an
            // unexpected persistence failure rather than one of the two known business outcomes above.
            // usp_Gift_ClaimIntoVault runs under XACT_ABORT, so the gift's Pending status is guaranteed to
            // have rolled back -- report it distinctly so the client isn't told its vault is full when it
            // was never checked.
            logger.LogWarning(ex,
                "Account {AccountId} gift claim index {GiftInfoIndex} (GiftId {GiftId}) failed unexpectedly",
                accountId, giftInfoIndex, pending[giftInfoIndex].GiftId);
            return new ClaimGiftResult(ClaimGiftOutcome.PersistenceFailure);
        }
    }
}
