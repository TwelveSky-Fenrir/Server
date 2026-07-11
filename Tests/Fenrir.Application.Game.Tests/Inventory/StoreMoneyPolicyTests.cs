using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

public class StoreMoneyPolicyTests
{
    [Fact]
    public void ResolveDeposit_Success_MovesExactAmountBothWays()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(100, 500, 50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceMoney);
        Assert.Equal(150, result.NewDestinationMoney);
    }

    [Fact]
    public void ResolveWithdraw_Success_MovesExactAmountBothWays()
    {
        var result = StoreMoneyPolicy.ResolveWithdraw(100, 500, 50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceMoney);
        Assert.Equal(150, result.NewDestinationMoney);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resolve_NonPositiveAmount_IsInvalid(long requestedAmount)
    {
        var result = StoreMoneyPolicy.ResolveDeposit(requestedAmount, 1000, 0);

        Assert.Equal(StoreMoneyPolicy.TransferOutcome.InvalidQuantity, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Resolve_AmountExceedsSource_IsInsufficient()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(1001, 1000, 0);

        Assert.Equal(StoreMoneyPolicy.TransferOutcome.InsufficientSource, result.Outcome);
    }

    [Fact]
    public void Resolve_AmountExactlyEqualsSource_Succeeds()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(1000, 1000, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NewSourceMoney);
        Assert.Equal(1000, result.NewDestinationMoney);
    }

    [Fact]
    public void Resolve_DestinationWouldOverflowCap_IsRejected()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(10,
            1_000_000_000, StoreMoneyPolicy.MaxMoney - 5);

        Assert.Equal(StoreMoneyPolicy.TransferOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void Resolve_DestinationExactlyAtCap_Succeeds()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(5,
            1_000_000_000, StoreMoneyPolicy.MaxMoney - 5);

        Assert.True(result.Succeeded);
        Assert.Equal(StoreMoneyPolicy.MaxMoney, result.NewDestinationMoney);
    }

    [Fact]
    public void Resolve_NoFixedPerRequestCapBeyondOverflowGuard()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(1_500_000_000,
            1_500_000_000, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1_500_000_000, result.NewDestinationMoney);
    }

    [Fact]
    public void Resolve_NeverTouchesBigMoneyPools()
    {
        var result = StoreMoneyPolicy.ResolveWithdraw(1, 1, 0);

        Assert.True(result.Succeeded);
    }
}
