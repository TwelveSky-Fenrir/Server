using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Accounts;

public interface IGiftRepository
{
    public ValueTask<ReadOnlyCollection<PendingGiftDto>> GetPendingByAccountAsync(int accountId, CancellationToken ct);

    public ValueTask<short> ClaimIntoVaultAsync(int giftId, int accountId, CancellationToken ct);

    public ValueTask<int> EnqueueAsync(int accountId, int? productId, int quantity, int value, CancellationToken ct);
}
