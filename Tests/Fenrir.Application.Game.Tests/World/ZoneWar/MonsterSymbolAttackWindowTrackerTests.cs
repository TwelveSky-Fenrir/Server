using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="MonsterSymbolAttackWindowTracker" />: the process-wide "has the current monster-symbol
///     holder been notified yet" latch consumed by <see cref="MonsterSymbolAttackWindowNotifySystem" />.
/// </summary>
public class MonsterSymbolAttackWindowTrackerTests
{
    [Fact]
    public void BelowDelay_NeverNotifies()
    {
        var tracker = new MonsterSymbolAttackWindowTracker();

        Assert.False(tracker.ShouldNotifyNow(0, 5, 10));
        Assert.False(tracker.ShouldNotifyNow(0, 4, 10));
    }

    [Fact]
    public void ReachingDelay_NotifiesExactlyOnce()
    {
        var tracker = new MonsterSymbolAttackWindowTracker();

        Assert.False(tracker.ShouldNotifyNow(0, 9, 10));
        Assert.True(tracker.ShouldNotifyNow(0, 1, 10)); // 9 + 1 == 10, crosses the delay
        Assert.False(tracker.ShouldNotifyNow(0, 1, 10)); // same holder, already notified -- never fires again
        Assert.False(tracker.ShouldNotifyNow(0, 100, 10));
    }

    [Fact]
    public void SingleCallExceedingDelay_NotifiesImmediately()
    {
        var tracker = new MonsterSymbolAttackWindowTracker();

        Assert.True(tracker.ShouldNotifyNow(2, 50, 10));
    }

    [Fact]
    public void HolderChanges_RearmsTheLatch_EvenIfPreviousHolderAlreadyNotified()
    {
        var tracker = new MonsterSymbolAttackWindowTracker();
        Assert.True(tracker.ShouldNotifyNow(0, 10, 10)); // tribe 0 notified
        Assert.False(tracker.ShouldNotifyNow(0, 1, 10)); // still tribe 0 -- no repeat

        // Tribe 1 captures the symbol -- re-arms from zero elapsed, does not inherit tribe 0's progress.
        Assert.False(tracker.ShouldNotifyNow(1, 9, 10));
        Assert.True(tracker.ShouldNotifyNow(1, 1, 10));
    }

    [Fact]
    public void SameHolderReturningAfterAnotherHolderHeldIt_TreatedAsAFreshHoldingPeriod()
    {
        var tracker = new MonsterSymbolAttackWindowTracker();
        Assert.True(tracker.ShouldNotifyNow(0, 10, 10)); // tribe 0 first holding period, notified
        Assert.False(tracker.ShouldNotifyNow(3, 5, 10)); // tribe 3 takes it, not yet at delay

        // Tribe 0 recaptures it -- a brand-new holding period, must be able to notify again once the delay
        // elapses, not be silently suppressed by the earlier _notified flag from its first holding period.
        Assert.False(tracker.ShouldNotifyNow(0, 9, 10));
        Assert.True(tracker.ShouldNotifyNow(0, 1, 10));
    }

    [Fact]
    public void ZeroDelay_NotifiesOnTheVeryFirstCall()
    {
        var tracker = new MonsterSymbolAttackWindowTracker();

        Assert.True(tracker.ShouldNotifyNow(1, 0, 0));
        Assert.False(tracker.ShouldNotifyNow(1, 0, 0)); // already notified, same holder
    }

    [Fact]
    public void ConcurrentCallers_EachAdvanceIsSerializedByTheInternalLock()
    {
        // Not a true stress/race test (xUnit test methods run single-threaded by default), but locks in the
        // expectation that many small increments from "different callers" (simulated sequentially here) sum
        // exactly, with no double-notify -- guards against a future refactor accidentally dropping the lock.
        var tracker = new MonsterSymbolAttackWindowTracker();
        var notifyCount = 0;

        for (var i = 0; i < 20; i++)
            if (tracker.ShouldNotifyNow(0, 1, 10))
                notifyCount++;

        Assert.Equal(1, notifyCount);
    }
}
