using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TitleContributionCostTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(105, 5)]
    [InlineData(212, 12)]
    [InlineData(1200, 0)]
    public void PortionOf_TakesModulo100(int storedTitle, int expectedPortion)
    {
        Assert.Equal(expectedPortion, TitleContributionCost.PortionOf(storedTitle));
    }

    [Theory]
    [InlineData(0, 800)]
    [InlineData(1, 1700)]
    [InlineData(10, 9300)]
    [InlineData(11, 10000)]
    public void PurchaseStepCost_ReturnsTableEntry(int portion, int expected)
    {
        Assert.Equal(expected, TitleContributionCost.PurchaseStepCost(portion));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(-1)]
    public void PurchaseStepCost_ZeroOutsidePurchaseRange(int portion)
    {
        Assert.Equal(0, TitleContributionCost.PurchaseStepCost(portion));
    }

    [Theory]
    [InlineData(1200, TitleContributionCost.RefundTypeFull)]
    [InlineData(8419, TitleContributionCost.RefundTypeFull)]
    [InlineData(1494, TitleContributionCost.RefundTypeReduced)]
    public void TryResolveRefundType_MapsScrollIds(int itemId, int expectedType)
    {
        Assert.True(TitleContributionCost.TryResolveRefundType(itemId, out var refundType));
        Assert.Equal(expectedType, refundType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(1201)]
    public void TryResolveRefundType_FalseForNonScrollItem(int itemId)
    {
        Assert.False(TitleContributionCost.TryResolveRefundType(itemId, out var refundType));
        Assert.Equal(-1, refundType);
    }

    [Theory]
    [InlineData(1, 800)]
    [InlineData(2, 2500)]
    [InlineData(5, 12600)]
    [InlineData(12, 55800)]
    public void CumulativeRefund_FullType_SumsEveryRankBelowPortion(int portion, int expected)
    {
        Assert.Equal(expected, TitleContributionCost.CumulativeRefund(portion, TitleContributionCost.RefundTypeFull));
    }

    [Fact]
    public void CumulativeRefund_FullType_IgnoresSortDigitsAboveThePortion()
    {
        Assert.Equal(12600, TitleContributionCost.CumulativeRefund(305, TitleContributionCost.RefundTypeFull));
    }

    [Theory]
    [InlineData(1, 560)]
    [InlineData(2, 1750)]
    [InlineData(12, 39060)]
    public void CumulativeRefund_ReducedType_Keeps70Percent(int portion, int expected)
    {
        Assert.Equal(expected,
            TitleContributionCost.CumulativeRefund(portion, TitleContributionCost.RefundTypeReduced));
    }

    [Theory]
    [InlineData(0, TitleContributionCost.RefundTypeFull)]
    [InlineData(13, TitleContributionCost.RefundTypeFull)]
    [InlineData(5, 2)]
    [InlineData(5, -1)]
    public void CumulativeRefund_ZeroForOutOfRangeInputs(int portion, int refundType)
    {
        Assert.Equal(0, TitleContributionCost.CumulativeRefund(portion, refundType));
    }

    [Fact]
    public void CumulativeRefund_Index12EntryIsNeverSummed()
    {
        Assert.NotEqual(65800, TitleContributionCost.CumulativeRefund(12, TitleContributionCost.RefundTypeFull));
        Assert.Equal(55800, TitleContributionCost.CumulativeRefund(12, TitleContributionCost.RefundTypeFull));
    }
}
