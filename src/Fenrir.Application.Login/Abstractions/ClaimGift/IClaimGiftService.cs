using Fenrir.Application.Login.Sessions;

namespace Fenrir.Application.Login.Abstractions.ClaimGift;

public enum ClaimGiftOutcome
{
    Success,

    GiftUnavailable,

    VaultFull,

    PersistenceFailure
}

public readonly record struct ClaimGiftResult(ClaimGiftOutcome Outcome);

public interface IClaimGiftService
{
    public ValueTask<ClaimGiftResult> ClaimGiftAsync(int accountId, int giftInfoIndex, GiftSlotBoard slots,
        CancellationToken cancellationToken);
}
