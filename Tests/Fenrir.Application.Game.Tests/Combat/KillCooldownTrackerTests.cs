using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>Pure-logic coverage of the C05 anti-farm gate -- no wall-clock dependency, every instant is synthetic.</summary>
public class KillCooldownTrackerTests
{
    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstKill_OfAnyPair_IsAlwaysAllowed()
    {
        var tracker = new KillCooldownTracker();

        Assert.True(tracker.TryRegisterKill(1, 2, Epoch));
    }

    [Fact]
    public void SecondKill_OfSamePair_WithinCooldown_IsRejected()
    {
        var tracker = new KillCooldownTracker();
        tracker.TryRegisterKill(1, 2, Epoch);

        var repeat = tracker.TryRegisterKill(1, 2, Epoch + TimeSpan.FromMinutes(5));

        Assert.False(repeat);
    }

    [Fact]
    public void SecondKill_OfSamePair_ExactlyAtCooldownBoundary_IsAllowed()
    {
        var tracker = new KillCooldownTracker();
        tracker.TryRegisterKill(1, 2, Epoch);

        var atBoundary = tracker.TryRegisterKill(1, 2, Epoch + KillCooldownTracker.DefaultCooldown);

        Assert.True(atBoundary);
    }

    [Fact]
    public void SecondKill_OfSamePair_AfterCooldownElapses_IsAllowedAndResetsTheWindow()
    {
        var tracker = new KillCooldownTracker();
        tracker.TryRegisterKill(1, 2, Epoch);

        var afterCooldown = tracker.TryRegisterKill(1, 2, Epoch + KillCooldownTracker.DefaultCooldown +
                                                          TimeSpan.FromSeconds(1));
        Assert.True(afterCooldown);

        // the window re-armed against the SECOND grant, not the first
        var immediatelyAfter = tracker.TryRegisterKill(1, 2,
            Epoch + KillCooldownTracker.DefaultCooldown + TimeSpan.FromSeconds(2));
        Assert.False(immediatelyAfter);
    }

    [Fact]
    public void DifferentDefender_IsNotGatedByAnUnrelatedPairsCooldown()
    {
        var tracker = new KillCooldownTracker();
        tracker.TryRegisterKill(1, 2, Epoch);

        var differentVictim = tracker.TryRegisterKill(1, 3, Epoch + TimeSpan.FromSeconds(1));

        Assert.True(differentVictim);
    }

    [Fact]
    public void ReversedPair_IsTrackedIndependently_NotSymmetric()
    {
        var tracker = new KillCooldownTracker();
        tracker.TryRegisterKill(1, 2, Epoch); // 1 kills 2

        // 2 killing 1 back immediately is a different ordered pair -- never blocked by the (1,2) entry above
        var revengeKill = tracker.TryRegisterKill(2, 1, Epoch + TimeSpan.FromSeconds(1));

        Assert.True(revengeKill);
    }

    [Fact]
    public void CustomCooldown_OverridesTheDefaultWindow()
    {
        var tracker = new KillCooldownTracker();
        var shortCooldown = TimeSpan.FromSeconds(30);
        tracker.TryRegisterKill(1, 2, Epoch, shortCooldown);

        var stillWithinShortWindow = tracker.TryRegisterKill(1, 2, Epoch + TimeSpan.FromSeconds(10), shortCooldown);
        var pastShortWindow = tracker.TryRegisterKill(1, 2, Epoch + TimeSpan.FromSeconds(31), shortCooldown);

        Assert.False(stillWithinShortWindow);
        Assert.True(pastShortWindow);
    }
}
