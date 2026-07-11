using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Simulation;

public sealed class Zone175EligibilityRulesTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void IsPresent_ExcludesTransferringAndHiding_ButNotDeath(bool isMovingZone, bool isHidden, bool expected)
    {
        Assert.Equal(expected, Zone175EligibilityRules.IsPresent(isMovingZone, isHidden));
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    public void IsRewardEligible_AddsTheNotDeadConditionOnTopOfPresence(bool isMovingZone, bool isHidden,
        bool isDead, bool expected)
    {
        Assert.Equal(expected, Zone175EligibilityRules.IsRewardEligible(isMovingZone, isHidden, isDead));
    }

    [Fact]
    public void DeadButPresentPlayer_CountsAsPresent_ButIsNotRewardEligible()
    {
        const bool isMovingZone = false;
        const bool isHidden = false;
        const bool isDead = true;

        Assert.True(Zone175EligibilityRules.IsPresent(isMovingZone, isHidden));
        Assert.False(Zone175EligibilityRules.IsRewardEligible(isMovingZone, isHidden, isDead));
    }
}
