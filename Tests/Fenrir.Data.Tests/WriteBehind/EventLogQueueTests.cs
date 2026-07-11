using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Data.Tests.WriteBehind;

public sealed class EventLogQueueTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    private static EventLogEntryTvp MakeEntry(short eventCode = 1, byte category = 0)
    {
        return new EventLogEntryTvp(eventCode, category, null, null, null, null, null, null, null, null, null, null,
            null,
            DateTime.UtcNow);
    }

    [Fact]
    public async Task RunAsync_FlushesAssoonAsAnEntryIsEnqueued_WithoutWaitingForTheLongInterval()
    {
        var flushed = new TaskCompletionSource<IReadOnlyList<EventLogEntryTvp>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var queue = new EventLogQueue(
            (batch, _) =>
            {
                flushed.TrySetResult(batch);
                return ValueTask.CompletedTask;
            },
            interval: TimeSpan.FromSeconds(30));

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
        Assert.True(flushedBatches.Count >= 3);

        cts.Cancel();
        await runTask.WaitAsync(BoundedWait);
    }

    [Fact]
    public async Task Enqueue_ReturnsFalse_OnceTheBoundedCapacityIsExhausted_AndInvokesOnDropped()
    {
        var dropped = 0;
        await using var queue = new EventLogQueue(
            (_, _) => ValueTask.CompletedTask,
            2,
            onDropped: count => Interlocked.Add(ref dropped, count));

        Assert.True(queue.Enqueue(MakeEntry()));
        Assert.True(queue.Enqueue(MakeEntry()));
        Assert.False(queue.Enqueue(MakeEntry()));

        Assert.Equal(1, dropped);
    }

    [Fact]
    public async Task RunAsync_WhenTheFlushCallbackThrows_DropsTheBatchInsteadOfRequeuing_AndKeepsRunning()
    {
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
            onFlushError: (ex, _) => firstAttemptFailed.TrySetResult(ex));

        using var cts = new CancellationTokenSource();
        var runTask = queue.RunAsync(cts.Token);

        Assert.True(queue.Enqueue(MakeEntry()));
        var observedFailure = await firstAttemptFailed.Task.WaitAsync(BoundedWait);
        Assert.IsType<InvalidOperationException>(observedFailure);

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

        var runTask = queue.RunAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(100));

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
