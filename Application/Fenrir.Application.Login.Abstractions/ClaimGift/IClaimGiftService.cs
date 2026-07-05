namespace Fenrir.Application.Login.Abstractions.ClaimGift;

public enum ClaimGiftOutcome
{
    IndexNotPending,
    ClaimFailed,
    Success
}

public readonly record struct ClaimGiftResult(ClaimGiftOutcome Outcome);

public interface IClaimGiftService
{
    public ValueTask<ClaimGiftResult> ClaimGiftAsync(int accountId, int giftInfoIndex,
        CancellationToken cancellationToken);
}
