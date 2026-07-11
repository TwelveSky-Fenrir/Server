using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TitleContributionCostTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(105, 5)] // (sort-1)*100 + portion 5
    [InlineData(212, 12)]
    [InlineData(1200, 0)] // 1200 % 100 == 0, not a title portion
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
    [InlineData(12)] // above the live purchase range
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
    [InlineData(1, 800)] // sum of ranks below portion 1 == table[0]
    [InlineData(2, 2500)] // table[0] + table[1]
    [InlineData(5, 12600)] // table[0..4]
    [InlineData(12, 55800)] // table[0..11] -- crucially EXCLUDES table[12] (10000)
    public void CumulativeRefund_FullType_SumsEveryRankBelowPortion(int portion, int expected)
    {
        Assert.Equal(expected, TitleContributionCost.CumulativeRefund(portion, TitleContributionCost.RefundTypeFull));
    }

    [Fact]
    public void CumulativeRefund_FullType_IgnoresSortDigitsAboveThePortion()
    {
        // stored title 305 -> portion 5; the leading "3" (title sort) must not affect the refund.
        Assert.Equal(12600, TitleContributionCost.CumulativeRefund(305, TitleContributionCost.RefundTypeFull));
    }

    [Theory]
    [InlineData(1, 560)] // 800 * 70 / 100
    [InlineData(2, 1750)] // 2500 * 70 / 100
    [InlineData(12, 39060)] // 55800 * 70 / 100
    public void CumulativeRefund_ReducedType_Keeps70Percent(int portion, int expected)
    {
        Assert.Equal(expected,
            TitleContributionCost.CumulativeRefund(portion, TitleContributionCost.RefundTypeReduced));
    }

    [Theory]
    [InlineData(0, TitleContributionCost.RefundTypeFull)] // portion below 1
    [InlineData(13, TitleContributionCost.RefundTypeFull)] // portion above the max of 12
    [InlineData(5, 2)] // refund type above 1
    [InlineData(5, -1)] // refund type below 0
    public void CumulativeRefund_ZeroForOutOfRangeInputs(int portion, int refundType)
    {
        Assert.Equal(0, TitleContributionCost.CumulativeRefund(portion, refundType));
    }

    [Fact]
    public void CumulativeRefund_Index12EntryIsNeverSummed()
    {
        // If the loop wrongly included table[12] (10000) at portion 12 the total would be 65800, not 55800.
        Assert.NotEqual(65800, TitleContributionCost.CumulativeRefund(12, TitleContributionCost.RefundTypeFull));
        Assert.Equal(55800, TitleContributionCost.CumulativeRefund(12, TitleContributionCost.RefundTypeFull));
    }
}
