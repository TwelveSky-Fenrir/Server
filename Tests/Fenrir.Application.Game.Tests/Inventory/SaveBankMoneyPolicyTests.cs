using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

/// <summary>
///     Coverage for <see cref="SaveBankMoneyPolicy" />, the pure policy behind tSort 231 (deposit money) and 232
///     (withdraw money). Does not depend on any dispatch wiring.
/// </summary>
public class SaveBankMoneyPolicyTests
{
    [Fact]
    public void ResolveDeposit_Success_MovesExactAmountBothWays()
    {
        var result = SaveBankMoneyPolicy.ResolveDeposit(100, 500, 50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceMoney);
        Assert.Equal(150, result.NewDestinationMoney);
    }

    [Fact]
    public void ResolveWithdraw_Success_MovesExactAmountBothWays()
    {
        var result = SaveBankMoneyPolicy.ResolveWithdraw(100, 500, 50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceMoney);
        Assert.Equal(150, result.NewDestinationMoney);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resolve_NonPositiveAmount_IsInvalid(long requestedAmount)
    {
        var result = SaveBankMoneyPolicy.ResolveDeposit(requestedAmount, 1000, 0);

        Assert.Equal(SaveBankMoneyPolicy.TransferOutcome.InvalidQuantity, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Resolve_AmountExceedsSource_IsInsufficient()
    {
        var result = SaveBankMoneyPolicy.ResolveDeposit(1001, 1000, 0);

        Assert.Equal(SaveBankMoneyPolicy.TransferOutcome.InsufficientSource, result.Outcome);
    }

    [Fact]
    public void Resolve_AmountExactlyEqualsSource_Succeeds()
    {
        var result = SaveBankMoneyPolicy.ResolveDeposit(1000, 1000, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NewSourceMoney);
        Assert.Equal(1000, result.NewDestinationMoney);
    }

    [Fact]
    public void Resolve_DestinationWouldOverflowCap_IsRejected()
    {
        var result = SaveBankMoneyPolicy.ResolveDeposit(10,
            1_000_000_000, SaveBankMoneyPolicy.MaxMoney - 5);

        Assert.Equal(SaveBankMoneyPolicy.TransferOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void Resolve_DestinationExactlyAtCap_Succeeds()
    {
        var result = SaveBankMoneyPolicy.ResolveDeposit(5,
            1_000_000_000, SaveBankMoneyPolicy.MaxMoney - 5);

        Assert.True(result.Succeeded);
        Assert.Equal(SaveBankMoneyPolicy.MaxMoney, result.NewDestinationMoney);
    }

    [Fact]
    public void Resolve_NoFixedPerRequestCapBeyondOverflowGuard()
    {
        // Unlike the 999 stackable-item cap, money has no fixed per-request ceiling besides the overflow guard.
        var result = SaveBankMoneyPolicy.ResolveDeposit(1_500_000_000,
            1_500_000_000, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1_500_000_000, result.NewDestinationMoney);
    }
}
