using System.Threading.Channels;
using Fenrir.Data.Abstractions.Game;

namespace Fenrir.Data.WriteBehind;

/// <summary>
///     Bounded-channel producer/consumer accumulator for the high-frequency EventLog write-behind path --
///     the channel-backed twin of <see cref="DirtyTracker{TKey}" />/<see cref="WriteBehindFlusher{TKey}" />
///     for a stream of independent, already-fully-formed rows rather than a per-key "what changed" set.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this is a separate implementation, not a reuse of DirtyTracker/WriteBehindFlusher:</b>
///         <see cref="DirtyFlags" /> is idempotent under merge (OR-ing the same flag twice is a no-op), so a
///         failed flush can safely re-<see cref="DirtyTracker{TKey}.MarkDirty" /> the whole drained batch back
///         in without any risk of double-counting or duplication. Every row here is instead an independent,
///         already-fully-formed fact (an enchant roll that already happened) with no natural key to merge on
///         -- there is nothing to "OR" two rows together into, and re-inserting the same row twice would
///         double the audit trail, not correct it. Re-queuing a failed batch would therefore require either
///         unbounded retry-buffer growth under a sustained DB outage, or an at-least-once/at-most-once
///         decision this path doesn't need to make. These are documented HIGH-FREQUENCY, LOW-STAKES events
///         (trades/currency/GM actions/account-security always go through
///         <c>IEventLogRepository.LogAsync</c> instead, never this queue) -- so a failed flush is logged and
///         the batch is DROPPED rather than retried, the same "no retry, accepted residual gap" posture
///         already used for monster-kill money grants (see <c>MonsterLootFlushHost</c>'s remarks).
///     </para>
///     <para>
///         Unlike <see cref="DirtyTracker{TKey}" />, which hand-maintains an Interlocked counter because
///         <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" />.Count takes every
///         partition lock, a bounded <see cref="Channel{T}" />'s <see cref="ChannelReader{T}.Count" /> is O(1)
///         by construction, so this class reads it directly instead of maintaining its own counter.
///     </para>
/// </remarks>
public sealed class EventLogQueue : IEventLogQueue, IAsyncDisposable
{
    public const int DefaultCapacity = 4096;
    public const int DefaultBatchSize = 256;
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

    private readonly int _batchSize;
    private readonly Channel<EventLogEntryTvp> _channel;
    private readonly Func<IReadOnlyList<EventLogEntryTvp>, CancellationToken, ValueTask> _flushCallback;
    private readonly TimeSpan _interval;
    private readonly TaskCompletionSource _loopExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<int>? _onDropped;
    private readonly Action<Exception>? _onFlushError;

    // Cancelled by DisposeAsync so a running loop exits before the timer/channel it awaits are torn down.
    private readonly CancellationTokenSource _shutdownCts = new();
    private int _disposed;
    private int _runStarted;

    public EventLogQueue(
        Func<IReadOnlyList<EventLogEntryTvp>, CancellationToken, ValueTask> flushCallback,
        int capacity = DefaultCapacity,
        int batchSize = DefaultBatchSize,
        TimeSpan? interval = null,
        Action<Exception>? onFlushError = null,
        Action<int>? onDropped = null)
    {
        _flushCallback = flushCallback;
        _batchSize = batchSize;
        _interval = interval ?? DefaultInterval;
        _onFlushError = onFlushError;
        _onDropped = onDropped;

        _channel = Channel.CreateBounded<EventLogEntryTvp>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            // NOT DropWrite: per the Channels source (BoundedChannelWriter.TryWrite), a DropWrite channel's
            // TryWrite returns TRUE even when it silently discards the item ("just ignore the item being
            // added but say we added it") -- Enqueue below could never observe or report a drop that way.
            // Wait mode's TryWrite, by contrast, returns false immediately and synchronously when full
            // (only WriteAsync actually awaits space) -- exactly the non-blocking, honestly-reported
            // backpressure signal this queue needs, since Enqueue only ever calls TryWrite, never WriteAsync.
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <inheritdoc />
    public bool Enqueue(EventLogEntryTvp entry)
    {
        if (_channel.Writer.TryWrite(entry))
            return true;

        _onDropped?.Invoke(1);
        return false;
    }

    /// <summary>Idempotent -- safe whether or not <see cref="RunAsync" /> ever started.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _channel.Writer.TryComplete();
        await _shutdownCts.CancelAsync().ConfigureAwait(false);

        if (Volatile.Read(ref _runStarted) != 0)
            await _loopExited.Task.ConfigureAwait(false);

        _shutdownCts.Dispose();
    }

    /// <summary>
    ///     Runs the drain loop until cancelled. Start once -- a second call throws (loops would race the same
    ///     timer/channel).
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _runStarted, 1) != 0)
            throw new InvalidOperationException(
                $"{nameof(EventLogQueue)}.{nameof(RunAsync)} must only be started once per instance.");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdownCts.Token);
            var loopCt = linkedCts.Token;
            using var timer = new PeriodicTimer(_interval);
            var reader = _channel.Reader;

            // Each of PeriodicTimer/ChannelReader allows one pending waiter; the losing WhenAny task is
            // re-awaited, not replaced (re-issuing would throw or miss a signal), same pattern as
            // WriteBehindFlusher.RunAsync.
            var tickTask = timer.WaitForNextTickAsync(loopCt).AsTask();
            var dataTask = reader.WaitToReadAsync(loopCt).AsTask();

            while (!loopCt.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(tickTask, dataTask).ConfigureAwait(false);

                if (loopCt.IsCancellationRequested)
                    break;

                if (completed == tickTask)
                    tickTask = timer.WaitForNextTickAsync(loopCt).AsTask();

                if (completed == dataTask)
                    dataTask = reader.WaitToReadAsync(loopCt).AsTask();

                if (reader.Count == 0)
                    continue; // bare tick with nothing buffered -- go back to waiting

                var batch = new List<EventLogEntryTvp>(Math.Min(reader.Count, _batchSize));
                while (batch.Count < _batchSize && reader.TryRead(out var item))
                    batch.Add(item);

                if (batch.Count > 0)
                    await FlushBatchAsync(batch, loopCt).ConfigureAwait(false);

                // If the channel still has a full batch's worth queued (backlog catch-up), dataTask above
                // is already complete-and-ready again on the next WhenAny, so the loop drains back-to-back
                // without waiting for the next timer tick.
            }
        }
        finally
        {
            _loopExited.TrySetResult();
        }
    }

    /// <summary>Logs and DROPS the batch on failure -- see class remarks for why this never re-queues.</summary>
    private async ValueTask FlushBatchAsync(IReadOnlyList<EventLogEntryTvp> batch, CancellationToken loopCt)
    {
        try
        {
            await _flushCallback(batch, loopCt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
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
