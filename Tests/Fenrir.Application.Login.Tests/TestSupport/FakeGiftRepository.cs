using System.Collections.ObjectModel;
using Fenrir.Data.Accounts;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for IGiftRepository: drives game.Gifts outcomes (a pending list of any length, a
// simulated claim failure) without a SQL container.
internal sealed class FakeGiftRepository : IGiftRepository
{
    private readonly Exception? _claimFault;
    private readonly List<PendingGiftDto> _pending;

    public FakeGiftRepository(IEnumerable<PendingGiftDto> pending, Exception? claimFault = null)
    {
        _pending = [.. pending];
        _claimFault = claimFault;
    }

    public int? LastClaimedGiftId { get; private set; }

    public ValueTask<ReadOnlyCollection<PendingGiftDto>> GetPendingByAccountAsync(int accountId, CancellationToken ct)
    {
        return ValueTask.FromResult(new ReadOnlyCollection<PendingGiftDto>(_pending));
    }

    public ValueTask<short> ClaimIntoVaultAsync(int giftId, int accountId, CancellationToken ct)
    {
        LastClaimedGiftId = giftId;
        return _claimFault is not null ? throw _claimFault : ValueTask.FromResult((short)0);
    }

    public static FakeGiftRepository WithPending(params (int GiftId, int? ProductId)[] gifts)
    {
        var rows = gifts.Select(g => new PendingGiftDto(g.GiftId, g.ProductId, 1, 0, DateTime.UtcNow));
        return new FakeGiftRepository(rows);
    }

    public static FakeGiftRepository Empty()
    {
        return new FakeGiftRepository([]);
    }

    public static FakeGiftRepository ThrowingOnClaim(Exception fault, params (int GiftId, int? ProductId)[] gifts)
    {
        var rows = gifts.Select(g => new PendingGiftDto(g.GiftId, g.ProductId, 1, 0, DateTime.UtcNow));
        return new FakeGiftRepository(rows, fault);
    }
}
