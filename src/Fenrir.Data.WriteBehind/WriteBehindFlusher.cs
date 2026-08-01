namespace Fenrir.Data.WriteBehind;

public interface IWriteBehindFlusher : IAsyncDisposable
{
    public void RequestImmediateFlush();
}

public sealed class WriteBehindFlusher<TKey> : IWriteBehindFlusher where TKey : notnull
{
    public const int DefaultEntityThreshold = 512;
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan ThresholdPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly int _entityThreshold;
    private readonly Func<IReadOnlyDictionary<TKey, DirtyFlags>, CancellationToken, ValueTask> _flushCallback;
    private readonly SemaphoreSlim _immediateFlushSignal = new(0, 1);
    private readonly TimeSpan _interval;

    private readonly TaskCompletionSource _loopExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<Exception>? _onFlushError;

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

    public void RequestImmediateFlush()
    {
        try
        {
            _immediateFlushSignal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await _shutdownCts.CancelAsync().ConfigureAwait(false);

        if (Volatile.Read(ref _runStarted) != 0)
            await _loopExited.Task.ConfigureAwait(false);

        _shutdownCts.Dispose();
        _timer.Dispose();
        _immediateFlushSignal.Dispose();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _runStarted, 1) != 0)
            throw new InvalidOperationException(
                $"{nameof(WriteBehindFlusher<>)}.{nameof(RunAsync)} must only be started once per instance.");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdownCts.Token);
            var loopCt = linkedCts.Token;

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

            var finalBatch = _tracker.DrainAll();
            if (finalBatch.Count > 0)
                await FlushBatchAsync(finalBatch, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _loopExited.TrySetResult();
        }
    }

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
            }
        }
    }
}
