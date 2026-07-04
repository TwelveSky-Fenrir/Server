namespace Fenrir.Data.WriteBehind;

// Non-generic handle so callers (disconnect, economic tx, level-up) don't need the closed generic type.
public interface IWriteBehindFlusher : IAsyncDisposable
{
    /// <summary>Non-blocking: only raises a signal, doesn't wait for the flush.</summary>
    public void RequestImmediateFlush();
}

/// <summary>Drains DirtyTracker on interval, immediate-flush request, or entityThreshold crossing -- whichever first.</summary>
/// <remarks>If flushCallback throws, the batch is re-merged into the tracker (not discarded) and the loop keeps running.</remarks>
public sealed class WriteBehindFlusher<TKey> : IWriteBehindFlusher where TKey : notnull
{
    public const int DefaultEntityThreshold = 512;
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

    // Poll cadence for noticing the entity threshold between ticks; never forces a flush by itself.
    private static readonly TimeSpan ThresholdPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly int _entityThreshold;
    private readonly Func<IReadOnlyDictionary<TKey, DirtyFlags>, CancellationToken, ValueTask> _flushCallback;
    private readonly SemaphoreSlim _immediateFlushSignal = new(0, 1);
    private readonly TimeSpan _interval;

    // Set when RunAsync's loop actually returns, so DisposeAsync can await it instead of racing it.
    private readonly TaskCompletionSource _loopExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<Exception>? _onFlushError;

    // Cancelled by DisposeAsync so a running loop exits before the timer/semaphore it awaits are disposed.
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly PeriodicTimer _timer;
    private readonly DirtyTracker<TKey> _tracker;
    private int _disposed;
    private int _runStarted;

    public WriteBehindFlusher(
        DirtyTracker<TKey> tracker,
        Func<IReadOnlyDictionary<TKey, DirtyFlags>, CancellationToken, ValueTask> flushCallback,
        TimeSpan? interval = null,
        int entityThreshold = DefaultEntityThreshold,
        Action<Exception>? onFlushError = null)
    {
        _tracker = tracker;
        _flushCallback = flushCallback;
        _interval = interval ?? DefaultInterval;
        _entityThreshold = entityThreshold;
        _onFlushError = onFlushError;
        _timer = new PeriodicTimer(_interval);
    }

    /// <inheritdoc />
    public void RequestImmediateFlush()
    {
        try
        {
            _immediateFlushSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A flush is already pending -- coalescing repeated requests into one is correct, not a bug.
        }
    }

    /// <summary>Cancels any running RunAsync loop and awaits its exit before disposing _timer/_immediateFlushSignal.</summary>
    /// <remarks>SemaphoreSlim.Dispose() is unsafe while a WaitAsync is outstanding; _shutdownCts unblocks the loop so it stops before disposal.</remarks>
    public async ValueTask DisposeAsync()
    {
        // Idempotent: CancellationTokenSource throws on a second Cancel, unlike PeriodicTimer/SemaphoreSlim.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await _shutdownCts.CancelAsync().ConfigureAwait(false);

        if (Volatile.Read(ref _runStarted) != 0)
            await _loopExited.Task.ConfigureAwait(false);

        _shutdownCts.Dispose();
        _timer.Dispose();
        _immediateFlushSignal.Dispose();
    }

    /// <summary>Runs the drain loop until cancelled. Start once -- a second call throws (loops would race the same timer/semaphore).</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _runStarted, 1) != 0)
            throw new InvalidOperationException(
                $"{nameof(WriteBehindFlusher<>)}.{nameof(RunAsync)} must only be started once per instance.");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdownCts.Token);
            var loopCt = linkedCts.Token;

            // Each of PeriodicTimer/SemaphoreSlim allows one pending waiter; the losing WhenAny task is
            // re-awaited, not replaced (re-issuing would throw or steal a permit).
            var tickTask = _timer.WaitForNextTickAsync(loopCt).AsTask();
            var signalTask = _immediateFlushSignal.WaitAsync(loopCt);

            while (!loopCt.IsCancellationRequested)
            {
                var pollTask = Task.Delay(ThresholdPollInterval, loopCt);

                var completed = await Task.WhenAny(tickTask, signalTask, pollTask).ConfigureAwait(false);

                if (loopCt.IsCancellationRequested)
                    break;

                var intervalElapsed = completed == tickTask;
                var immediateRequested = completed == signalTask;

                if (intervalElapsed)
                    tickTask = _timer.WaitForNextTickAsync(loopCt).AsTask();
                if (immediateRequested)
                    signalTask = _immediateFlushSignal.WaitAsync(loopCt);

                var thresholdReached = _tracker.Count >= _entityThreshold;

                if ((!intervalElapsed && !immediateRequested && !thresholdReached) || _tracker.Count <= 0) continue;
                var batch = _tracker.DrainAll();
                if (batch.Count > 0)
                    await FlushBatchAsync(batch, loopCt).ConfigureAwait(false);
            }
        }
        finally
        {
            // Set even if the loop throws unexpectedly, so DisposeAsync never hangs waiting on an already-exited loop.
            _loopExited.TrySetResult();
        }
    }

    /// <summary>Re-merges the batch into the tracker if flushCallback throws, instead of losing it.</summary>
    private async ValueTask FlushBatchAsync(IReadOnlyDictionary<TKey, DirtyFlags> batch, CancellationToken loopCt)
    {
        try
        {
            await _flushCallback(batch, loopCt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            foreach (var (key, flags) in batch)
                _tracker.MarkDirty(key, flags);

            try
            {
                _onFlushError?.Invoke(ex);
            }
            catch
            {
                // Best-effort: a broken logger must not take the drain loop down.
            }
        }
    }
}
