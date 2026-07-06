using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

/// <summary>
///     Coverage for <see cref="StoreMoneyPolicy" />, the pure policy behind tSort 226 (deposit money) and 227
///     (withdraw money). Does not depend on any dispatch wiring.
/// </summary>
public class StoreMoneyPolicyTests
{
    [Fact]
    public void ResolveDeposit_Success_MovesExactAmountBothWays()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(requestedAmount: 100, walletMoney: 500, storeMoney: 50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceMoney);
        Assert.Equal(150, result.NewDestinationMoney);
    }

    [Fact]
    public void ResolveWithdraw_Success_MovesExactAmountBothWays()
    {
        var result = StoreMoneyPolicy.ResolveWithdraw(requestedAmount: 100, storeMoney: 500, walletMoney: 50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceMoney);
        Assert.Equal(150, result.NewDestinationMoney);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resolve_NonPositiveAmount_IsInvalid(long requestedAmount)
    {
        var result = StoreMoneyPolicy.ResolveDeposit(requestedAmount, walletMoney: 1000, storeMoney: 0);

        Assert.Equal(StoreMoneyPolicy.TransferOutcome.InvalidQuantity, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Resolve_AmountExceedsSource_IsInsufficient()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(requestedAmount: 1001, walletMoney: 1000, storeMoney: 0);

        Assert.Equal(StoreMoneyPolicy.TransferOutcome.InsufficientSource, result.Outcome);
    }

    [Fact]
    public void Resolve_AmountExactlyEqualsSource_Succeeds()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(requestedAmount: 1000, walletMoney: 1000, storeMoney: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NewSourceMoney);
        Assert.Equal(1000, result.NewDestinationMoney);
    }

    [Fact]
    public void Resolve_DestinationWouldOverflowCap_IsRejected()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(requestedAmount: 10,
            walletMoney: 1_000_000_000, storeMoney: StoreMoneyPolicy.MaxMoney - 5);

        Assert.Equal(StoreMoneyPolicy.TransferOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void Resolve_DestinationExactlyAtCap_Succeeds()
    {
        var result = StoreMoneyPolicy.ResolveDeposit(requestedAmount: 5,
            walletMoney: 1_000_000_000, storeMoney: StoreMoneyPolicy.MaxMoney - 5);

        Assert.True(result.Succeeded);
        Assert.Equal(StoreMoneyPolicy.MaxMoney, result.NewDestinationMoney);
    }

    [Fact]
    public void Resolve_NoFixedPerRequestCapBeyondOverflowGuard()
    {
        // Unlike the 999 stackable-item cap, money has no fixed per-request ceiling besides the overflow guard.
        var result = StoreMoneyPolicy.ResolveDeposit(requestedAmount: 1_500_000_000,
            walletMoney: 1_500_000_000, storeMoney: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1_500_000_000, result.NewDestinationMoney);
    }

    [Fact]
    public void Resolve_NeverTouchesBigMoneyPools()
    {
        // Documentation-as-test: StoreMoneyPolicy's signature has no BigMoney/BigStoreMoney parameter at all --
        // the 1B pool is explicitly out of scope for tSort 226/227 and must not be inferred from this policy.
        var result = StoreMoneyPolicy.ResolveWithdraw(requestedAmount: 1, storeMoney: 1, walletMoney: 0);

        Assert.True(result.Succeeded);
    }
}
