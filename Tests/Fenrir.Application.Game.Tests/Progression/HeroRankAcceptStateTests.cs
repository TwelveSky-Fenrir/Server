using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class HeroRankAcceptStateTests
{
    [Fact]
    public void IsClaimable_OnlyUnclaimed()
    {
        Assert.True(HeroRankAcceptStateRules.IsClaimable(HeroRankAcceptState.Unclaimed));
        Assert.False(HeroRankAcceptStateRules.IsClaimable(HeroRankAcceptState.ClaimedPendingSettlement));
        Assert.False(HeroRankAcceptStateRules.IsClaimable(HeroRankAcceptState.Settled));
    }

    [Fact]
    public void IsClaimed_TrueForBothClaimedStates()
    {
        Assert.False(HeroRankAcceptStateRules.IsClaimed(HeroRankAcceptState.Unclaimed));
        Assert.True(HeroRankAcceptStateRules.IsClaimed(HeroRankAcceptState.ClaimedPendingSettlement));
        Assert.True(HeroRankAcceptStateRules.IsClaimed(HeroRankAcceptState.Settled));
    }

    [Theory]
    [InlineData(HeroRankAcceptState.ClaimedPendingSettlement, HeroRankAcceptState.Settled)]
    [InlineData(HeroRankAcceptState.Unclaimed, HeroRankAcceptState.Unclaimed)]
    [InlineData(HeroRankAcceptState.Settled, HeroRankAcceptState.Settled)]
    public void PromoteToSettled_OnlyAdvancesPendingSlots(HeroRankAcceptState input, HeroRankAcceptState expected)
    {
        Assert.Equal(expected, HeroRankAcceptStateRules.PromoteToSettled(input));
    }

    [Fact]
    public void PromoteToSettled_IsIdempotent()
    {
        var once = HeroRankAcceptStateRules.PromoteToSettled(HeroRankAcceptState.ClaimedPendingSettlement);
        var twice = HeroRankAcceptStateRules.PromoteToSettled(once);

        Assert.Equal(HeroRankAcceptState.Settled, twice);
    }

    [Theory]
    [InlineData(null, HeroRankAcceptState.Unclaimed)]
    [InlineData(false, HeroRankAcceptState.Unclaimed)]
    [InlineData(true, HeroRankAcceptState.Settled)]
    public void FromClaimedFlag_MapsFenrirBoolModel(bool? flag, HeroRankAcceptState expected)
    {
        Assert.Equal(expected, HeroRankAcceptStateRules.FromClaimedFlag(flag));
    }

    [Theory]
    [InlineData(HeroRankAcceptState.Unclaimed, false)]
    [InlineData(HeroRankAcceptState.ClaimedPendingSettlement, true)]
    [InlineData(HeroRankAcceptState.Settled, true)]
    public void ToClaimedFlag_AnyClaimedStateIsTrue(HeroRankAcceptState state, bool expected)
    {
        Assert.Equal(expected, HeroRankAcceptStateRules.ToClaimedFlag(state));
    }

    [Fact]
    public void ClaimedFlag_RoundTripsThroughTheTriState()
    {
        Assert.True(HeroRankAcceptStateRules.ToClaimedFlag(HeroRankAcceptStateRules.FromClaimedFlag(true)));
        Assert.False(HeroRankAcceptStateRules.ToClaimedFlag(HeroRankAcceptStateRules.FromClaimedFlag(false)));
    }
}
