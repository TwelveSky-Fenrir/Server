using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Commerce;

/// <summary>
///     Executable spec for <c>IGiftRepository.EnqueueAsync</c>'s "mint one gift" contract -- see that
///     member's XML remarks for the full legacy-dead-code disclosure this shape was designed after
///     (<c>MyDB::UpdateGift</c>, Server/ts25playuser/S08_MyDB.cpp:331-381, never reached in any shipped
///     build). No production caller wires this yet: deciding the actual trigger (a specific cash-item
///     purchase, a future GM command) is explicitly out of scope for this contract, so these tests exercise
///     the repository surface directly against <see cref="FakeGiftRepository" /> rather than a handler or
///     service.
/// </summary>
public sealed class GiftEnqueueContractTests
{
    [Fact]
    public async Task EnqueueAsync_ReturnsAnAssignedGiftId_AndForwardsEveryArgumentVerbatim()
    {
        var repository = new FakeGiftRepository();

        var giftId = await repository.EnqueueAsync(1000000123, 4200, 3, 7, CancellationToken.None);

        Assert.True(giftId > 0);
        var recorded = Assert.Single(repository.Enqueued);
        Assert.Equal(1000000123, recorded.AccountId);
        Assert.Equal(4200, recorded.ProductId);
        Assert.Equal(3, recorded.Quantity);
        Assert.Equal(7, recorded.Value);
    }

    [Fact]
    public async Task EnqueueAsync_MintsADistinctGiftIdPerCall_EvenForIdenticalArguments()
    {
        var repository = new FakeGiftRepository();

        var first = await repository.EnqueueAsync(1000000001, 1, 1, 0, CancellationToken.None);
        var second = await repository.EnqueueAsync(1000000001, 1, 1, 0, CancellationToken.None);

        Assert.NotEqual(first, second);
        Assert.Equal(2, repository.Enqueued.Count);
    }

    [Theory]
    [InlineData(0, 0)] // zero quantity/value: no floor is applied, matching the dead legacy shape.
    [InlineData(-5, -1)] // negative quantity/value: copied through unchanged, never rejected.
    public async Task EnqueueAsync_DoesNotValidateQuantityOrValue(int quantity, int value)
    {
        var repository = new FakeGiftRepository();

        var giftId = await repository.EnqueueAsync(1000000001, productId: null, quantity, value,
            CancellationToken.None);

        Assert.True(giftId > 0);
        var recorded = Assert.Single(repository.Enqueued);
        Assert.Equal(quantity, recorded.Quantity);
        Assert.Equal(value, recorded.Value);
    }

    [Fact]
    public async Task EnqueueAsync_AcceptsANullProductId()
    {
        // ProductId is an unenforced reference in both the dead legacy shape and the new game.Gifts schema
        // (no catalog check observed anywhere in the cited range) -- null must round-trip cleanly.
        var repository = new FakeGiftRepository();

        await repository.EnqueueAsync(1000000001, productId: null, quantity: 1, value: 0, CancellationToken.None);

        var recorded = Assert.Single(repository.Enqueued);
        Assert.Null(recorded.ProductId);
    }
}
