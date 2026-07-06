using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Data.Tests.WriteBehind;

// Pure in-memory unit tests for EventLogQueue: the flush callback is faked, no database needed. Docker
// (and therefore the SqlServerFixture every other Fenrir.Data.Tests class depends on -- see e.g.
// CashProcTests) was not reachable when these were written, so game.usp_EventLog_Insert/InsertBatch and
// EventLogRepository itself are not exercised here; add an EventLogProcTests mirroring CashProcTests'
// shape once a real SQL Server 2025 container is available for a test run.
public sealed class EventLogQueueTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    private static EventLogEntryTvp MakeEntry(short eventCode = 1, byte category = 0) =>
        new(eventCode, category, null, null, null, null, null, null, null, null, null, null, null,
            DateTime.UtcNow);

    [Fact]
    public async Task RunAsync_FlushesAssoonAsAnEntryIsEnqueued_WithoutWaitingForTheLongInterval()
    {
        // reader.WaitToReadAsync completes as soon as ANY item is available, so -- unlike
        // WriteBehindFlusher, which needs an explicit RequestImmediateFlush or an entity-count threshold --
        // EventLogQueue flushes on data availability alone, racing the timer via Task.WhenAny.
        var flushed = new TaskCompletionSource<IReadOnlyList<EventLogEntryTvp>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var queue = new EventLogQueue(
            (batch, _) =>
            {
                flushed.TrySetResult(batch);
                return ValueTask.CompletedTask;
            },
            interval: TimeSpan.FromSeconds(30)); // long enough that only the enqueue can explain a fast flush

        using var cts = new CancellationTokenSource();
        var runTask = queue.RunAsync(cts.Token);

        var entry = MakeEntry(42);
        Assert.True(queue.Enqueue(entry));

        var batch = await flushed.Task.WaitAsync(BoundedWait);
        Assert.Equal(42, Assert.Single(batch).EventCode);

        cts.Cancel();
        await runTask.WaitAsync(BoundedWait);
    }

    [Fact]
    public async Task RunAsync_DrainsBacklogAcrossMultipleFlushes_WhenMoreThanOneBatchIsQueued()
    {
        const int batchSize = 4;
        const int entryCount = 10;

        var flushedBatches = new List<int>();
        var allFlushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var totalFlushed = 0;

        await using var queue = new EventLogQueue(
            (batch, _) =>
            {
                lock (flushedBatches)
                {
                    flushedBatches.Add(batch.Count);
                    totalFlushed += batch.Count;
                    if (totalFlushed >= entryCount)
                        allFlushed.TrySetResult();
                }

                return ValueTask.CompletedTask;
            },
            batchSize: batchSize,
            interval: TimeSpan.FromSeconds(30));

        using var cts = new CancellationTokenSource();
        var runTask = queue.RunAsync(cts.Token);

        for (var i = 0; i < entryCount; i++)
            Assert.True(queue.Enqueue(MakeEntry((short)i)));

        await allFlushed.Task.WaitAsync(BoundedWait);

        Assert.Equal(entryCount, flushedBatches.Sum());
        Assert.All(flushedBatches, count => Assert.True(count <= batchSize));
        Assert.True(flushedBatches.Count >= 3); // 10 entries at batchSize 4 needs at least 3 flushes

        cts.Cancel();
        await runTask.WaitAsync(BoundedWait);
    }

    [Fact]
    public async Task Enqueue_ReturnsFalse_OnceTheBoundedCapacityIsExhausted_AndInvokesOnDropped()
    {
        // No RunAsync started -- nothing ever drains the channel, so capacity fills up deterministically.
        var dropped = 0;
        await using var queue = new EventLogQueue(
            (_, _) => ValueTask.CompletedTask,
            capacity: 2,
            onDropped: count => Interlocked.Add(ref dropped, count));

        Assert.True(queue.Enqueue(MakeEntry()));
        Assert.True(queue.Enqueue(MakeEntry()));
        Assert.False(queue.Enqueue(MakeEntry())); // capacity 2 is now full

        Assert.Equal(1, dropped);
    }

    [Fact]
    public async Task RunAsync_WhenTheFlushCallbackThrows_DropsTheBatchInsteadOfRequeuing_AndKeepsRunning()
    {
        // Unlike WriteBehindFlusher (which re-merges a failed batch back into the DirtyTracker), EventLogQueue
        // has no idempotent merge key for independent audit rows, so a failed flush is logged and dropped.
        var attempt = 0;
        var firstAttemptFailed =
            new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondAttemptSucceeded = new TaskCompletionSource<IReadOnlyList<EventLogEntryTvp>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var queue = new EventLogQueue(
            (batch, _) =>
            {
                if (Interlocked.Increment(ref attempt) == 1)
                    throw new InvalidOperationException("simulated transient SQL failure");

                secondAttemptSucceeded.TrySetResult(batch);
                return ValueTask.CompletedTask;
            },
            interval: TimeSpan.FromMilliseconds(50),
            onFlushError: ex => firstAttemptFailed.TrySetResult(ex));

        using var cts = new CancellationTokenSource();
        var runTask = queue.RunAsync(cts.Token);

        Assert.True(queue.Enqueue(MakeEntry(1)));
        var observedFailure = await firstAttemptFailed.Task.WaitAsync(BoundedWait);
        Assert.IsType<InvalidOperationException>(observedFailure);

        // The failed batch is gone -- a second, independent entry proves the loop is still alive and that
        // the dropped batch was never requeued ahead of it.
        Assert.True(queue.Enqueue(MakeEntry(2)));
        var batch = await secondAttemptSucceeded.Task.WaitAsync(BoundedWait);
        Assert.Equal(2, Assert.Single(batch).EventCode);

        cts.Cancel();
        await runTask.WaitAsync(BoundedWait);
    }

    [Fact]
    public async Task DisposeAsync_StopsAStillRunningLoop_EvenIfTheCallersTokenWasNeverCancelled()
    {
        var queue = new EventLogQueue((_, _) => ValueTask.CompletedTask, interval: TimeSpan.FromMilliseconds(20));

        // Token is never cancelled -- only DisposeAsync's internal shutdown signal can stop the loop.
        var runTask = queue.RunAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(100)); // let a few loop iterations actually happen first

        await queue.DisposeAsync().AsTask().WaitAsync(BoundedWait);

        await runTask.WaitAsync(BoundedWait);
        Assert.True(runTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task RunAsync_CalledASecondTime_ThrowsInsteadOfRacingTheFirstLoop()
    {
        await using var queue = new EventLogQueue((_, _) => ValueTask.CompletedTask,
            interval: TimeSpan.FromSeconds(30));

        using var cts = new CancellationTokenSource();
        var firstRun = queue.RunAsync(cts.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.RunAsync(CancellationToken.None));

        cts.Cancel();
        await firstRun.WaitAsync(BoundedWait);
    }
}
