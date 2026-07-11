using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Simulation;

public sealed class Zone175EligibilityRulesTests
{
    [Theory]
    [InlineData(false, false, true)] // not transferring, not hidden -> present
    [InlineData(true, false, false)] // mid zone-transition -> not present
    [InlineData(false, true, false)] // hiding -> not present
    [InlineData(true, true, false)]
    public void IsPresent_ExcludesTransferringAndHiding_ButNotDeath(bool isMovingZone, bool isHidden, bool expected)
    {
        Assert.Equal(expected, Zone175EligibilityRules.IsPresent(isMovingZone, isHidden));
    }

    [Theory]
    [InlineData(false, false, false, true)] // present and alive -> eligible
    [InlineData(false, false, true, false)] // present but dead -> NOT eligible (the one extra condition)
    [InlineData(true, false, false, false)] // transferring -> not eligible
    [InlineData(false, true, false, false)] // hiding -> not eligible
    public void IsRewardEligible_AddsTheNotDeadConditionOnTopOfPresence(bool isMovingZone, bool isHidden,
        bool isDead, bool expected)
    {
        Assert.Equal(expected, Zone175EligibilityRules.IsRewardEligible(isMovingZone, isHidden, isDead));
    }

    [Fact]
    public void DeadButPresentPlayer_CountsAsPresent_ButIsNotRewardEligible()
    {
        // The source contract's "wave-clear vs reward eligibility differ by one condition" edge case: a
        // dead-but-present player keeps a wave alive yet earns no reward for its clear.
        const bool isMovingZone = false;
        const bool isHidden = false;
        const bool isDead = true;

        Assert.True(Zone175EligibilityRules.IsPresent(isMovingZone, isHidden));
        Assert.False(Zone175EligibilityRules.IsRewardEligible(isMovingZone, isHidden, isDead));
    }
}
