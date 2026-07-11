using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class CpTicketResolverTests
{
    [Theory]
    [InlineData(691, 5)]
    [InlineData(692, 10)]
    [InlineData(693, 15)]
    [InlineData(694, 20)]
    [InlineData(1444, 500)]
    [InlineData(1447, 1000)]
    [InlineData(8433, 1000)]
    [InlineData(1448, 500)]
    [InlineData(8432, 500)]
    [InlineData(1449, 100)]
    [InlineData(8431, 100)]
    [InlineData(1499, 5000)]
    [InlineData(8434, 5000)]
    public void TryGetPerUnitAmount_EveryHandledId_ResolvesItsDocumentedAmount(int itemId, int expectedAmount)
    {
        Assert.True(CpTicketResolver.TryGetPerUnitAmount(itemId, out var amount));
        Assert.Equal(expectedAmount, amount);
    }

    [Fact]
    public void TryGetPerUnitAmount_UnrecognizedId_ReturnsFalse()
    {
        Assert.False(CpTicketResolver.TryGetPerUnitAmount(999999, out _));
    }

    [Fact]
    public void HandledItemIds_ContainsEveryDocumentedId()
    {
        var ids = CpTicketResolver.HandledItemIds.ToHashSet();
        foreach (var expected in new[] { 691, 692, 693, 694, 1444, 1447, 8433, 1448, 8432, 1449, 8431, 1499, 8434 })
            Assert.Contains(expected, ids);
        Assert.Equal(13, ids.Count);
    }

    [Fact]
    public void Resolve_PlainClick_UsesExactlyOneUnit()
    {
        var result = CpTicketResolver.Resolve(691, requestedValue: 0, stackQuantity: 10, currentContributionPoints: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.UnitsConsumed);
        Assert.Equal(5, result.CreditedAmount);
        Assert.Equal(5, result.NewContributionPoints);
    }

    [Fact]
    public void Resolve_BulkRequest_ClampedToOnHandStock()
    {
        var result = CpTicketResolver.Resolve(691, requestedValue: 999, stackQuantity: 4, currentContributionPoints: 100);

        Assert.True(result.Succeeded);
        Assert.Equal(4, result.UnitsConsumed);
        Assert.Equal(20, result.CreditedAmount);
        Assert.Equal(120, result.NewContributionPoints);
    }

    [Fact]
    public void Resolve_BulkRequest_ClampedToDuplicationCeiling()
    {
        var result = CpTicketResolver.Resolve(1499, requestedValue: 5000, stackQuantity: 5000,
            currentContributionPoints: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(BulkUseCoercion.MaxStackQuantity, result.UnitsConsumed);
        Assert.Equal(BulkUseCoercion.MaxStackQuantity * 5000L, result.CreditedAmount);
    }

    [Fact]
    public void Resolve_RequestedValueBelowOne_FlooredToOneUnit()
    {
        var result = CpTicketResolver.Resolve(692, requestedValue: 0, stackQuantity: 10, currentContributionPoints: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.UnitsConsumed);
        Assert.Equal(10, result.CreditedAmount);
    }

    [Fact]
    public void Resolve_UnrecognizedItemId_ReportsInsufficientQuantity()
    {
        var result = CpTicketResolver.Resolve(999999, requestedValue: 1, stackQuantity: 10,
            currentContributionPoints: 100);

        Assert.Equal(CpTicketResolver.Outcome.InsufficientQuantity, result.Outcome);
        Assert.Equal(100, result.NewContributionPoints);
    }

    [Fact]
    public void Resolve_ZeroStackQuantity_ReportsInsufficientQuantity()
    {
        var result = CpTicketResolver.Resolve(691, requestedValue: 1, stackQuantity: 0, currentContributionPoints: 100);

        Assert.Equal(CpTicketResolver.Outcome.InsufficientQuantity, result.Outcome);
        Assert.Equal(100, result.NewContributionPoints);
    }

    [Fact]
    public void Resolve_WouldExceedCeiling_Rejects_ContributionPointsUntouched()
    {
        var result = CpTicketResolver.Resolve(1499, requestedValue: 1, stackQuantity: 1,
            currentContributionPoints: BankedCounterMath.GlobalCeiling);

        Assert.Equal(CpTicketResolver.Outcome.WouldExceedCeiling, result.Outcome);
        Assert.Equal(BankedCounterMath.GlobalCeiling, result.NewContributionPoints);
        Assert.Equal(0, result.UnitsConsumed);
    }
}
