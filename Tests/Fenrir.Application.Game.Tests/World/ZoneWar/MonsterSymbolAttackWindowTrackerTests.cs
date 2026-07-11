using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

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
        Assert.True(tracker.ShouldNotifyNow(0, 1, 10));
        Assert.False(tracker.ShouldNotifyNow(0, 1, 10));
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
        Assert.True(tracker.ShouldNotifyNow(0, 10, 10));
        Assert.False(tracker.ShouldNotifyNow(0, 1, 10));

        Assert.False(tracker.ShouldNotifyNow(1, 9, 10));
        Assert.True(tracker.ShouldNotifyNow(1, 1, 10));
    }

    [Fact]
    public void SameHolderReturningAfterAnotherHolderHeldIt_TreatedAsAFreshHoldingPeriod()
    {
        var tracker = new MonsterSymbolAttackWindowTracker();
        Assert.True(tracker.ShouldNotifyNow(0, 10, 10));
        Assert.False(tracker.ShouldNotifyNow(3, 5, 10));

        Assert.False(tracker.ShouldNotifyNow(0, 9, 10));
        Assert.True(tracker.ShouldNotifyNow(0, 1, 10));
    }

    [Fact]
    public void ZeroDelay_NotifiesOnTheVeryFirstCall()
    {
        var tracker = new MonsterSymbolAttackWindowTracker();

        Assert.True(tracker.ShouldNotifyNow(1, 0, 0));
        Assert.False(tracker.ShouldNotifyNow(1, 0, 0));
    }

    [Fact]
    public void ConcurrentCallers_EachAdvanceIsSerializedByTheInternalLock()
    {
        var tracker = new MonsterSymbolAttackWindowTracker();
        var notifyCount = 0;

        for (var i = 0; i < 20; i++)
            if (tracker.ShouldNotifyNow(0, 1, 10))
                notifyCount++;

        Assert.Equal(1, notifyCount);
    }
}
