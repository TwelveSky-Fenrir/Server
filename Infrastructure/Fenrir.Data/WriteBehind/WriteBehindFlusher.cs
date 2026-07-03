namespace Fenrir.Data.WriteBehind;

/// <summary>
///     Non-generic handle to a running <see cref="WriteBehindFlusher{TKey}" />: anything that needs to force
///     an out-of-band flush -- disconnect, an economic transaction, a level-up (§10.5) -- can depend on this
///     instead of the closed generic type.
/// </summary>
public interface IWriteBehindFlusher : IAsyncDisposable
{
    /// <summary>
    ///     Requests a flush on the next loop iteration instead of waiting for the periodic interval. Never
    ///     blocks the calling thread -- it only raises a signal and returns immediately.
    /// </summary>
    public void RequestImmediateFlush();
}

/// <summary>
///     Drains a <see cref="DirtyTracker{TKey}" /> every <c>interval</c>, whenever
///     <see cref="RequestImmediateFlush" /> is called, or whenever the tracker crosses
///     <c>entityThreshold</c> dirty entities -- whichever happens first -- and hands the batch to the
///     supplied flush callback (architecture reference §10.5).
/// </summary>
/// <remarks>
///     M1 scope: this is only the reusable mechanism. Nothing calls <see cref="DirtyTracker{TKey}.MarkDirty" />
///     for a real player yet -- the zone-actor tick that would (which character, in which zone) belongs to
///     Phase 6. Until then this class has no live producer wired in, only a shape other code can build against.
/// </remarks>
/// <remarks>
///     Failure contract (§14.3, "SQL indisponible 30 s : le shard continue de jouer, le flusher accumule et
///     rattrape") : if <c>flushCallback</c> throws, the drained batch is re-merged back into the tracker
///     instead of being discarded, and the loop keeps running -- one bad flush (transient timeout, deadlock,
///     SQL momentarily down) never loses state and never ends the drain loop. The <c>onFlushError</c>
///     constructor parameter is an optional, best-effort observation hook for a caller that wants to log or
///     count these failures; it is never required for correctness.
/// </remarks>
public sealed class WriteBehindFlusher<TKey> : IWriteBehindFlusher where TKey : notnull
{
    public const int DefaultEntityThreshold = 512;
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

    // How often we re-check DirtyTracker.Count between timer ticks / immediate signals, so crossing the
    // entity threshold doesn't have to wait out the rest of a 5 s interval. This cadence only decides how
    // promptly the threshold is *noticed* -- it never forces a flush of a non-empty, under-threshold batch.
    private static readonly TimeSpan ThresholdPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly int _entityThreshold;
    private readonly Func<IReadOnlyDictionary<TKey, DirtyFlags>, CancellationToken, ValueTask> _flushCallback;
    private readonly SemaphoreSlim _immediateFlushSignal = new(0, 1);
    private readonly TimeSpan _interval;

    // Set once RunAsync's loop has actually returned (including after re-merging an in-flight batch on the
    // way out), so DisposeAsync can await it instead of racing it. Never set at all if RunAsync is never
    // started, which is why DisposeAsync only awaits this when _runStarted says there is something to await.
    private readonly TaskCompletionSource _loopExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<Exception>? _onFlushError;

    // Cancelled by DisposeAsync so a RunAsync loop in progress unblocks and exits *before* the timer/semaphore
    // it awaits get disposed -- see DisposeAsync's remarks.
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

    /// <summary>
    ///     Requests cancellation of any <see cref="RunAsync" /> loop in progress and waits for it to actually
    ///     return before disposing <see cref="_timer" />/<see cref="_immediateFlushSignal" />.
    /// </summary>
    /// <remarks>
    ///     <see cref="SemaphoreSlim.Dispose()" /> is documented as unsafe while another thread has an
    ///     outstanding <see cref="SemaphoreSlim.WaitAsync(CancellationToken)" /> on the same instance, and
    ///     <see cref="RunAsync" /> keeps exactly such a wait alive across iterations -- so disposing the
    ///     semaphore/timer out from under a still-running loop (e.g. a shutdown race, before the caller's own
    ///     token is observed as cancelled) is the bug this coordinates against. <see cref="_shutdownCts" /> is
    ///     linked into <see cref="RunAsync" />'s token regardless of what the caller passed in, so cancelling
    ///     it here unblocks the loop even if the caller never cancels its own token; awaiting
    ///     <see cref="_loopExited" /> then guarantees the loop has stopped touching either field before they
    ///     are disposed. If <see cref="RunAsync" /> was never started, there is nothing to wait for.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        // Idempotent: hosting code frequently disposes both explicitly and via `await using`, and unlike
        // PeriodicTimer/SemaphoreSlim.Dispose() (already safe to call twice), CancellationTokenSource throws
        // ObjectDisposedException on a second Cancel -- guard it the same way the rest of this method stays safe.
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
    ///     Runs the drain loop until <paramref name="ct" /> is cancelled (or <see cref="DisposeAsync" /> is
    ///     called). Intended to be started once (e.g. by a hosted service) and left running until shutdown --
    ///     calling this a second time on the same instance throws, since two loops would race over the same
    ///     timer/semaphore.
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

            // PeriodicTimer and SemaphoreSlim each support exactly one pending waiter. A task that loses a
            // WhenAny race is kept and re-awaited on the next iteration instead of being replaced -- re-issuing
            // WaitForNextTickAsync while a prior call is still pending throws, and abandoning a pending
            // WaitAsync would silently steal a permit the moment it completes unobserved.
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

                if ((intervalElapsed || immediateRequested || thresholdReached) && _tracker.Count > 0)
                {
                    var batch = _tracker.DrainAll();
                    if (batch.Count > 0)
                        await FlushBatchAsync(batch, loopCt).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            // Runs even if the loop above throws unexpectedly (it shouldn't -- FlushBatchAsync never lets a
            // callback failure escape) so DisposeAsync can never hang waiting on a loop that has, in fact,
            // already exited.
            _loopExited.TrySetResult();
        }
    }

    /// <summary>
    ///     Invokes <see cref="_flushCallback" /> and, if it throws, re-merges <paramref name="batch" /> back
    ///     into <see cref="_tracker" /> instead of losing it -- see the failure-contract remarks on this type.
    /// </summary>
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
                // The observation hook is best-effort only -- a broken logger must not take the drain loop
                // down any more than a broken SQL connection does.
            }
        }
    }
}
