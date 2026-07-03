using Fenrir.Data.WriteBehind;

namespace Fenrir.Data.Tests.WriteBehind;

/// <summary>
///     Pure in-memory unit tests for <see cref="WriteBehindFlusher{TKey}" />: the mechanism needs no database
///     (the flush callback is faked), so its timing/threshold/failure semantics are covered here instead of
///     against SQL Server. Every bounded wait below uses a generous timeout so the test fails loudly on a
///     real regression instead of hanging.
/// </summary>
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
            1_000); // large enough that only the interval can trigger this flush

        using var cts = new CancellationTokenSource();
        var runTask = flusher.RunAsync(cts.Token);

        var batch = await flushed.Task.WaitAsync(BoundedWait);

        Assert.Equal(DirtyFlags.Position, Assert.Single(batch).Value);
        Assert.Equal(0, tracker.Count); // the flusher drains before invoking the callback

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
            TimeSpan.FromSeconds(30), // long enough that only RequestImmediateFlush can explain a fast flush
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
        tracker.MarkDirty(3, DirtyFlags.Position); // crosses entityThreshold = 3

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

        // The failed batch must have been re-merged back into the tracker -- nothing lost -- and by the time
        // onFlushError fires the re-merge has already happened (it runs before the error hook), so this read
        // is not racing FlushBatchAsync's catch block.
        Assert.Equal(1, tracker.Count);

        // A second immediate flush, now that the callback is "fixed" (attempt 2), must succeed and pick up
        // exactly the re-merged entry -- proving the drain loop survived the first failure instead of dying.
        flusher.RequestImmediateFlush();
        var batch = await secondAttemptSucceeded.Task.WaitAsync(BoundedWait);

        Assert.Equal(DirtyFlags.Position, Assert.Single(batch).Value);
        Assert.Equal(0, tracker.Count);

        cts.Cancel();
        await runTask.WaitAsync(BoundedWait);
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

        // CancellationToken.None: RunAsync's own token is never cancelled, so only DisposeAsync's internal
        // shutdown signal (linked into the loop) can stop it -- exactly the race the fix coordinates.
        var runTask = flusher.RunAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(100)); // let a few loop iterations actually happen first

        await flusher.DisposeAsync().AsTask().WaitAsync(BoundedWait);

        // DisposeAsync only returns after the loop has actually exited, so this must already be done.
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
