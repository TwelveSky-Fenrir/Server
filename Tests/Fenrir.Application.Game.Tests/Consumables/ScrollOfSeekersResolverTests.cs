using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class ScrollOfSeekersResolverTests
{
    [Theory]
    [InlineData(ScrollOfSeekersResolver.ScrollOfSeekersItemId, 180)]
    [InlineData(ScrollOfSeekersResolver.ScrollOfSeekers3HourItemId, 180)]
    [InlineData(ScrollOfSeekersResolver.ScrollOfSeekersLItemId, 900)]
    [InlineData(ScrollOfSeekersResolver.ScrollOfSeekers15HourItemId, 900)]
    [InlineData(ScrollOfSeekersResolver.ScrollOfSeekers15HourAltItemId, 900)]
    public void AmountFor_MatchesTheDocumentedPerIdSplit(int itemId, int expectedAmount)
    {
        Assert.Equal(expectedAmount, ScrollOfSeekersResolver.AmountFor(itemId));
    }

    [Fact]
    public void Resolve_DefaultGroupId_AddsOneHundredEighty()
    {
        var result = ScrollOfSeekersResolver.Resolve(ScrollOfSeekersResolver.ScrollOfSeekersItemId, 10);

        Assert.True(result.Succeeded);
        Assert.Equal(180, result.CreditedAmount);
        Assert.Equal(190, result.NewZoneTime);
    }

    [Fact]
    public void Resolve_OverrideGroupId_AddsNineHundred()
    {
        var result = ScrollOfSeekersResolver.Resolve(ScrollOfSeekersResolver.ScrollOfSeekersLItemId, 10);

        Assert.True(result.Succeeded);
        Assert.Equal(900, result.CreditedAmount);
        Assert.Equal(910, result.NewZoneTime);
    }

    [Fact]
    public void Resolve_WouldExceedCeiling_Rejects_CounterUntouched()
    {
        var result = ScrollOfSeekersResolver.Resolve(ScrollOfSeekersResolver.ScrollOfSeekersLItemId,
            BankedCounterMath.GlobalCeiling);

        Assert.Equal(ScrollOfSeekersResolver.Outcome.WouldExceedCeiling, result.Outcome);
        Assert.Equal(BankedCounterMath.GlobalCeiling, result.NewZoneTime);
    }

    [Fact]
    public void HandledItemIds_IsExactlyTheFiveDocumentedIds()
    {
        var ids = ScrollOfSeekersResolver.HandledItemIds.ToHashSet();

        Assert.Equal(5, ids.Count);
        foreach (var expected in new[] { 1124, 1187, 7016, 8409, 8410 })
            Assert.Contains(expected, ids);
    }
}
