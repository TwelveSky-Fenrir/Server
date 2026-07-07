namespace Fenrir.Data.WriteBehind;

// Non-generic handle so callers (disconnect, economic tx, level-up) don't need the closed generic type.
public interface IWriteBehindFlusher : IAsyncDisposable
{
    /// <summary>
    ///     Non-blocking: only raises a signal, doesn't wait for the flush. The caller gets no signal of
    ///     whether/when the resulting drain completes, succeeds, or which entities it captured -- treat a call
    ///     to this method as "a drain will happen sooner than the next periodic interval," never as a
    ///     durability guarantee for any one specific entity (see <c>ICharacterWriteBehindFlusher.FlushCharacterNowAsync</c>
    ///     for the synchronous, single-entity alternative used where that guarantee matters, e.g. a character
    ///     disconnect).
    /// </summary>
    public void RequestImmediateFlush();
}

/// <summary>Drains DirtyTracker on interval, immediate-flush request, or entityThreshold crossing -- whichever first.</summary>
/// <remarks>
///     If flushCallback throws, the batch is re-merged into the tracker (not discarded) and the loop keeps running.
///     <para>
///         <b>Process-kill residual risk (acknowledged, not mitigated here):</b> a graceful shutdown drains
///         whatever is dirty at the instant cancellation is observed (<see cref="RunAsync" />'s final
///         unconditional <c>DrainAll</c> below), and <c>ICharacterWriteBehindFlusher.FlushCharacterNowAsync</c>
///         additionally covers a single character's own graceful disconnect synchronously. Neither runs on a
///         true (non-graceful) process kill -- SIGKILL/task-kill-9-equivalent, or a hard crash -- since no C#
///         code executes at all once the process is gone. In that scenario, every entity dirtied since the
///         LAST successful drain is lost, bounded by whichever of this instance's own triggers would have
///         fired first: <paramref name="interval" /> (wall-clock periodic trigger), or the moment
///         <c>tracker.Count</c> crosses <paramref name="entityThreshold" /> (polled every
///         <see cref="ThresholdPollInterval" />, itself not a forcing function by itself). This is a bounded,
///         known tradeoff -- not a silent gap -- and is deliberately not addressed by a WAL/journal here; see
///         each concrete <see cref="IWriteBehindFlusher" />-hosting <c>BackgroundService</c>'s own remarks for
///         its instance's specific bound and whether its interval was judged cheap enough to tighten.
///     </para>
/// </remarks>
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
    /// <remarks>
    ///     SemaphoreSlim.Dispose() is unsafe while a WaitAsync is outstanding; _shutdownCts unblocks the loop so it stops
    ///     before disposal.
    /// </remarks>
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

    /// <summary>
    ///     Runs the drain loop until cancelled. Start once -- a second call throws (loops would race the same
    ///     timer/semaphore).
    /// </summary>
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

            // Best-effort final drain so a graceful shutdown doesn't abandon whatever's still dirty at this
            // instant -- same "loop exits on cancellation, then one more unconditional flush" idiom already
            // used by TowerWarWriteBehindHost/WorldStateWriteBehindHost/MonsterBossRespawnWriteBehindHost/
            // HeroRankPointsWriteBehindHost (Application/Fenrir.Application.Game.Hosting), and the direct fix
            // for the parity gap against legacy's force-save-then-poll-until-drained shutdown sequence
            // (Server/ts25playuser/S07_MyGame01.cpp:317-372): legacy force-saves and confirms every still-valid
            // session's save is drained before terminating; this is that same guarantee for whatever this
            // tracker still holds the instant cancellation is observed, instead of silently dropping it. loopCt
            // is already cancelled by this point (that's what ended the loop above), so a cancellation-aware DB
            // call made with it would fault immediately -- CancellationToken.None matches the same already-
            // established choice in the sibling hosts cited above. FlushBatchAsync's own re-merge-on-failure
            // path already logs/reports via onFlushError rather than losing the batch if this attempt fails.
            var finalBatch = _tracker.DrainAll();
            if (finalBatch.Count > 0)
                await FlushBatchAsync(finalBatch, CancellationToken.None).ConfigureAwait(false);
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
