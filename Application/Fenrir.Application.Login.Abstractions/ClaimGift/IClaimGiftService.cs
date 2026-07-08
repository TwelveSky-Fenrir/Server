namespace Fenrir.Application.Login.Abstractions.ClaimGift;

/// <summary>
///     Outward result of a op21 CL_WANT_GIFT_SEND claim attempt, matching the three distinct legacy failure
///     signals plus success (Server/ts25login/S04_MyWork02.cpp:1435-1480) instead of collapsing every failure
///     into one generic code.
/// </summary>
public enum ClaimGiftOutcome
{
    Success,

    /// <summary>
    ///     No gift sits at the requested list position any more: either <c>GiftInfoIndex</c> was already past
    ///     the pending list's current length, or the gift existed when the pending list was read but was
    ///     claimed/removed by a racing request before the repository's own claim call could flip its status
    ///     (SQL 50220). Both cases mean the same thing to the client: this slot has nothing to claim, never
    ///     "your vault is full".
    /// </summary>
    GiftUnavailable,

    /// <summary>The gift itself was claimable, but the account's shared vault has no free slot (SQL 50274).</summary>
    VaultFull,

    /// <summary>
    ///     The claim failed for a reason other than the two expected business outcomes above (e.g. a dropped
    ///     connection or timeout mid-transaction). <c>usp_Gift_ClaimIntoVault</c> runs under XACT_ABORT, so the
    ///     gift's Pending status is guaranteed to have rolled back -- it can be retried.
    /// </summary>
    PersistenceFailure
}

public readonly record struct ClaimGiftResult(ClaimGiftOutcome Outcome);

public interface IClaimGiftService
{
    public ValueTask<ClaimGiftResult> ClaimGiftAsync(int accountId, int giftInfoIndex,
        CancellationToken cancellationToken);
}
