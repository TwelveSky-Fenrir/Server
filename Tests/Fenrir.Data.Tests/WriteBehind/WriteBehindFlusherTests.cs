using Fenrir.Data.WriteBehind;

namespace Fenrir.Data.Tests.WriteBehind;

public sealed class WriteBehindFlusherTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task RunAsync_FlushesOnTheInterval_AndEmptiesTheTrackerAfterwards()
    {
        var tracker = new DirtyTracker<int>();
        tracker.MarkDirty(1, DirtyFlags.Position);

        var flushed = new TaskCompletionSource<IReadOnlyDictionary<int, DirtyFlags>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var flusher = new WriteBehindFlusher<int>(
            tracker,
            (batch, _) =>
            {
                flushed.TrySetResult(batch);
                return ValueTask.CompletedTask;
            },
            TimeSpan.FromMilliseconds(100),
            1_000);

        using var cts = new CancellationTokenSource();
        var runTask = flusher.RunAsync(cts.Token);

        var batch = await flushed.Task.WaitAsync(BoundedWait);

        Assert.Equal(DirtyFlags.Position, Assert.Single(batch).Value);
        Assert.Equal(0, tracker.Count);

        cts.Cancel();
        await runTask.WaitAsync(BoundedWait);
    }

    [Fact]
    public async Task RequestImmediateFlush_TriggersABatch_WithoutWaitingForTheLongInterval()
    {
        var tracker = new DirtyTracker<int>();
        tracker.MarkDirty(42, DirtyFlags.Vitals);

        var flushed = new TaskCompletionSource<IReadOnlyDictionary<int, DirtyFlags>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var flusher = new WriteBehindFlusher<int>(
            tracker,
            (batch, _) =>
            {
                flushed.TrySetResult(batch);
                return ValueTask.CompletedTask;
            },
            TimeSpan.FromSeconds(30),
            1_000);

        using var cts = new CancellationTokenSource();
        var runTask = flusher.RunAsync(cts.Token);

        flusher.RequestImmediateFlush();

        var batch = await flushed.Task.WaitAsync(BoundedWait);
        Assert.Equal(42, Assert.Single(batch).Key);

        cts.Cancel();
        await runTask.WaitAsync(BoundedWait);
    }

    [Fact]
    public async Task RunAsync_FlushesAssoonAsTheEntityThresholdIsCrossed_WithoutRequestImmediateFlushOrTheInterval()
    {
        var tracker = new DirtyTracker<int>();

        var flushed = new TaskCompletionSource<IReadOnlyDictionary<int, DirtyFlags>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var flusher = new WriteBehindFlusher<int>(
            tracker,
            (batch, _) =>
            {
                flushed.TrySetResult(batch);
                return ValueTask.CompletedTask;
            },
            TimeSpan.FromSeconds(30),
            3);

        using var cts = new CancellationTokenSource();
        var runTask = flusher.RunAsync(cts.Token);

        tracker.MarkDirty(1, DirtyFlags.Position);
        tracker.MarkDirty(2, DirtyFlags.Position);
        tracker.MarkDirty(3, DirtyFlags.Position);

        var batch = await flushed.Task.WaitAsync(BoundedWait);
        Assert.Equal(3, batch.Count);

        cts.Cancel();
        await runTask.WaitAsync(BoundedWait);
    }

    [Fact]
    public async Task RunAsync_WhenTheFlushCallbackThrows_ReMergesTheBatchInsteadOfLosingIt_AndKeepsRunning()
    {
        var tracker = new DirtyTracker<int>();
        tracker.MarkDirty(7, DirtyFlags.Position);

        var attempt = 0;
        var firstAttemptFailed =
            new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondAttemptSucceeded = new TaskCompletionSource<IReadOnlyDictionary<int, DirtyFlags>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var flusher = new WriteBehindFlusher<int>(
            tracker,
            (batch, _) =>
            {
                if (Interlocked.Increment(ref attempt) == 1)
                    throw new InvalidOperationException("simulated transient SQL failure");

                secondAttemptSucceeded.TrySetResult(batch);
                return ValueTask.CompletedTask;
            },
            TimeSpan.FromSeconds(30),
            1_000,
            ex => firstAttemptFailed.TrySetResult(ex));

        using var cts = new CancellationTokenSource();
        var runTask = flusher.RunAsync(cts.Token);

        flusher.RequestImmediateFlush();
        var observedFailure = await firstAttemptFailed.Task.WaitAsync(BoundedWait);
        Assert.IsType<InvalidOperationException>(observedFailure);

        Assert.Equal(1, tracker.Count);

        flusher.RequestImmediateFlush();
        var batch = await secondAttemptSucceeded.Task.WaitAsync(BoundedWait);

        Assert.Equal(DirtyFlags.Position, Assert.Single(batch).Value);
        Assert.Equal(0, tracker.Count);

        cts.Cancel();
        await runTask.WaitAsync(BoundedWait);
    }

    [Fact]
    public async Task RunAsync_OnCancellation_PerformsAFinalDrainOfWhateverIsStillDirty()
    {
        var tracker = new DirtyTracker<int>();
        tracker.MarkDirty(99, DirtyFlags.Position | DirtyFlags.Vitals);

        var flushCount = 0;
        IReadOnlyDictionary<int, DirtyFlags>? finalBatch = null;

        var flusher = new WriteBehindFlusher<int>(
            tracker,
            (batch, _) =>
            {
                Interlocked.Increment(ref flushCount);
                finalBatch = batch;
                return ValueTask.CompletedTask;
            },
            TimeSpan.FromSeconds(30),
            1_000);

        using var cts = new CancellationTokenSource();
        var runTask = flusher.RunAsync(cts.Token);

        cts.Cancel();
        await runTask.WaitAsync(BoundedWait);
        await flusher.DisposeAsync();

        Assert.Equal(1, flushCount);
        Assert.Equal(0, tracker.Count);
        Assert.Equal(DirtyFlags.Position | DirtyFlags.Vitals, Assert.Single(finalBatch!).Value);
    }

    [Fact]
    public async Task DisposeAsync_StopsAStillRunningLoop_EvenIfTheCallersTokenWasNeverCancelled()
    {
        var tracker = new DirtyTracker<int>();

        await using var flusher = new WriteBehindFlusher<int>(
            tracker,
            (_, _) => ValueTask.CompletedTask,
            TimeSpan.FromMilliseconds(20),
            1_000);

        var runTask = flusher.RunAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        await flusher.DisposeAsync().AsTask().WaitAsync(BoundedWait);

        await runTask.WaitAsync(BoundedWait);
        Assert.True(runTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task RunAsync_CalledASecondTime_ThrowsInsteadOfRacingTheFirstLoop()
    {
        var tracker = new DirtyTracker<int>();

        await using var flusher = new WriteBehindFlusher<int>(
            tracker,
            (_, _) => ValueTask.CompletedTask,
            TimeSpan.FromSeconds(30),
            1_000);

        using var cts = new CancellationTokenSource();
        var firstRun = flusher.RunAsync(cts.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => flusher.RunAsync(CancellationToken.None));

        cts.Cancel();
        await firstRun.WaitAsync(BoundedWait);
    }
}
