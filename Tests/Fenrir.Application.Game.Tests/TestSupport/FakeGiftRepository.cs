using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Accounts;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IGiftRepository, scoped to the EnqueueAsync ("mint one gift") surface that
///     this test project exercises. GetPendingByAccountAsync/ClaimIntoVaultAsync are not driven by any
///     Game-side production code yet -- deciding the actual purchase-reward/GM-command trigger is out of
///     scope for the EnqueueAsync contract itself (see IGiftRepository.EnqueueAsync's remarks) -- so they
///     are minimal, order-preserving pass-throughs here rather than fully modeled.
/// </summary>
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
