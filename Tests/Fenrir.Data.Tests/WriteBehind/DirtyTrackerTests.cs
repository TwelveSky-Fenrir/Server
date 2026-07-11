using System.Collections.Concurrent;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Data.Tests.WriteBehind;

public sealed class DirtyTrackerTests
{
    [Fact]
    public void MarkDirty_NewKey_AddsEntry_AndCountReflectsIt()
    {
        var tracker = new DirtyTracker<int>();

        tracker.MarkDirty(1, DirtyFlags.Position);

        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void MarkDirty_SameKeyTwice_OrsFlagsTogether_WithoutChangingCount()
    {
        var tracker = new DirtyTracker<int>();

        tracker.MarkDirty(1, DirtyFlags.Position);
        tracker.MarkDirty(1, DirtyFlags.Vitals);

        Assert.Equal(1, tracker.Count);

        var drained = tracker.DrainAll();
        var entry = Assert.Single(drained);
        Assert.Equal(1, entry.Key);
        Assert.Equal(DirtyFlags.Position | DirtyFlags.Vitals, entry.Value);
    }

    [Fact]
    public void MarkDirty_DifferentKeys_TracksEachIndependently()
    {
        var tracker = new DirtyTracker<int>();

        tracker.MarkDirty(1, DirtyFlags.Position);
        tracker.MarkDirty(2, DirtyFlags.Vitals);

        Assert.Equal(2, tracker.Count);

        var drained = tracker.DrainAll();
        Assert.Equal(2, drained.Count);
        Assert.Equal(DirtyFlags.Position, drained[1]);
        Assert.Equal(DirtyFlags.Vitals, drained[2]);
    }

    [Fact]
    public void DrainAll_OnAnEmptyTracker_ReturnsEmpty_AndCountStaysZero()
    {
        var tracker = new DirtyTracker<int>();

        var drained = tracker.DrainAll();

        Assert.Empty(drained);
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void DrainAll_EmptiesTheTracker_AndResetsCountToZero()
    {
        var tracker = new DirtyTracker<int>();
        tracker.MarkDirty(1, DirtyFlags.Position);
        tracker.MarkDirty(2, DirtyFlags.Position);

        var drained = tracker.DrainAll();

        Assert.Equal(2, drained.Count);
        Assert.Equal(0, tracker.Count);
        Assert.Empty(tracker.DrainAll());
    }

    [Fact]
    public void DrainAll_ThenMarkDirtyAgain_CountTracksTheNewCycleCorrectly()
    {
        var tracker = new DirtyTracker<int>();
        tracker.MarkDirty(1, DirtyFlags.Position);
        tracker.DrainAll();

        tracker.MarkDirty(2, DirtyFlags.Vitals);
        tracker.MarkDirty(3, DirtyFlags.Vitals);

        Assert.Equal(2, tracker.Count);
    }

    [Fact]
    public async Task MarkDirty_And_DrainAll_UnderConcurrency_NeverLosesAKey_AndCountNeverGoesNegative()
    {
        const int keyCount = 32;
        const int writerTaskCount = 8;
        const int markCallsPerWriter = 4_000;

        var tracker = new DirtyTracker<int>();
        var everMarked = new ConcurrentDictionary<int, byte>();
        var everDrained = new ConcurrentDictionary<int, byte>();
        using var stopDraining = new CancellationTokenSource();

        var drainTask = Task.Run(async () =>
        {
            while (!stopDraining.IsCancellationRequested)
            {
                foreach (var key in tracker.DrainAll().Keys)
                    everDrained[key] = 0;

                await Task.Yield();
            }

            foreach (var key in tracker.DrainAll().Keys)
                everDrained[key] = 0;
        });

        var writers = Enumerable.Range(0, writerTaskCount).Select(seed => Task.Run(() =>
        {
            var random = new Random(seed);
            for (var i = 0; i < markCallsPerWriter; i++)
            {
                var key = random.Next(keyCount);
                everMarked[key] = 0;
                tracker.MarkDirty(key, DirtyFlags.Position);
            }
        }));

        await Task.WhenAll(writers);
        stopDraining.Cancel();
        await drainTask;

        Assert.Equal(0, tracker.Count);
        Assert.True(
            everMarked.Keys.All(everDrained.ContainsKey),
            "Every key ever marked dirty must have been observed in at least one DrainAll -- none may be lost.");
    }
}
