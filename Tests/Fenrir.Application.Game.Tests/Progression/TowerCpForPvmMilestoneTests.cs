using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerCpForPvmMilestoneTests
{
    [Fact]
    public void RegisterKill_LevelAppropriateKill_IncrementsTheCounterByOne()
    {
        var result = TowerCpForPvmMilestone.RegisterKill(5, 50,
            0, 50);

        Assert.Equal(6, result.UpdatedCounter);
        Assert.False(result.MilestoneReached);
    }

    [Fact]
    public void RegisterKill_GapOfExactlyTen_DoesNotCount()
    {
        // ReturnFixedLevel(50) - 40 == 10, the boundary the contract states as "strictly under 10 counts".
        var result = TowerCpForPvmMilestone.RegisterKill(5, 50,
            0, 40);

        Assert.Equal(5, result.UpdatedCounter);
        Assert.False(result.MilestoneReached);
    }

    [Fact]
    public void RegisterKill_GapOfNine_StillCounts()
    {
        var result = TowerCpForPvmMilestone.RegisterKill(5, 50,
            0, 41);

        Assert.Equal(6, result.UpdatedCounter);
    }

    [Fact]
    public void RegisterKill_UsesTheSumOfLevel1AndLevel2_NotLevel1Alone()
    {
        // Level1 alone (50) - monster 55 would be a *favorable* gap (fine); but the milestone's eligibility
        // basis is level1+level2, so a high level2 pushes the effective level well past the monster's.
        var result = TowerCpForPvmMilestone.RegisterKill(0, 50,
            60, 1);

        // ReturnFixedLevel(110) - 1 = 109, far past the <10 window -- must not count.
        Assert.Equal(0, result.UpdatedCounter);
        Assert.False(result.MilestoneReached);
    }

    [Fact]
    public void RegisterKill_The1000thKill_ResetsTheCounterAndReportsTheMilestone()
    {
        var result = TowerCpForPvmMilestone.RegisterKill(999, 50,
            0, 50);

        Assert.Equal(0, result.UpdatedCounter);
        Assert.True(result.MilestoneReached);
    }

    [Fact]
    public void RegisterKill_KillNumber1001IfSomehowStartedPastTheThreshold_StillResetsAndFires()
    {
        var result = TowerCpForPvmMilestone.RegisterKill(1000, 50,
            0, 50);

        Assert.Equal(0, result.UpdatedCounter);
        Assert.True(result.MilestoneReached);
    }

    [Fact]
    public void ComputeReward_NoTowerBonus_IsJustTheBaseKillCp()
    {
        Assert.Equal(1, TowerCpForPvmMilestone.ComputeReward(0));
    }

    [Fact]
    public void ComputeReward_WithTowerBonus_AddsItFlatOnTopOfTheBase()
    {
        Assert.Equal(3, TowerCpForPvmMilestone.ComputeReward(2));
    }

    [Fact]
    public void ComputeReward_NegativeTowerBonus_IsClampedToZeroContribution()
    {
        Assert.Equal(1, TowerCpForPvmMilestone.ComputeReward(-5));
    }
}
