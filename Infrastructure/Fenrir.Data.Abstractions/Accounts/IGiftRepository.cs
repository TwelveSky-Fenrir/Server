using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Accounts;

// Interface (unlike AccountRepository) so GiftListHandler/ClaimGiftHandler can be unit-tested without a SQL container.
public interface IGiftRepository
{
    public ValueTask<ReadOnlyCollection<PendingGiftDto>> GetPendingByAccountAsync(int accountId, CancellationToken ct);

    /// <summary>
    ///     Atomically claims the gift into the shared vault. Throws SQL 50220 (not found/not owned/claimed) or 50274
    ///     (vault full, 28 slots).
    /// </summary>
    public ValueTask<short> ClaimIntoVaultAsync(int giftId, int accountId, CancellationToken ct);
}
