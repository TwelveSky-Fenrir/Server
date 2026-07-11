using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Accounts;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeGiftRepository : IGiftRepository
{
    private readonly List<PendingGiftDto> _pending = [];
    private int _nextGiftId = 1;

    public List<(int AccountId, int? ProductId, int Quantity, int Value)> Enqueued { get; } = [];

    public ValueTask<ReadOnlyCollection<PendingGiftDto>> GetPendingByAccountAsync(int accountId, CancellationToken ct)
    {
        return ValueTask.FromResult(new ReadOnlyCollection<PendingGiftDto>(_pending));
    }

    public ValueTask<short> ClaimIntoVaultAsync(int giftId, int accountId, CancellationToken ct)
    {
        throw new NotImplementedException("Not exercised by any Game-side test yet.");
    }

    public ValueTask<int> EnqueueAsync(int accountId, int? productId, int quantity, int value, CancellationToken ct)
    {
        Enqueued.Add((accountId, productId, quantity, value));

        var giftId = _nextGiftId++;
        _pending.Add(new PendingGiftDto(giftId, productId, quantity, value, DateTime.UtcNow));
        return ValueTask.FromResult(giftId);
    }
}
