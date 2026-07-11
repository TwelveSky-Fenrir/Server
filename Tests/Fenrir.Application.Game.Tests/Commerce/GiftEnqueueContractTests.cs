using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Commerce;

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
    [InlineData(0, 0)]
    [InlineData(-5, -1)]
    public async Task EnqueueAsync_DoesNotValidateQuantityOrValue(int quantity, int value)
    {
        var repository = new FakeGiftRepository();

        var giftId = await repository.EnqueueAsync(1000000001, null, quantity, value,
            CancellationToken.None);

        Assert.True(giftId > 0);
        var recorded = Assert.Single(repository.Enqueued);
        Assert.Equal(quantity, recorded.Quantity);
        Assert.Equal(value, recorded.Value);
    }

    [Fact]
    public async Task EnqueueAsync_AcceptsANullProductId()
    {
        var repository = new FakeGiftRepository();

        await repository.EnqueueAsync(1000000001, null, 1, 0, CancellationToken.None);

        var recorded = Assert.Single(repository.Enqueued);
        Assert.Null(recorded.ProductId);
    }
}
