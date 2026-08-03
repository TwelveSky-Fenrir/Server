using System.Threading.Channels;
using Fenrir.Data.Abstractions.Game;

namespace Fenrir.Data.WriteBehind;

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
    private readonly Action<Exception, int>? _onFlushError;

    private readonly CancellationTokenSource _shutdownCts = new();
    private int _disposed;
    private int _runStarted;

    public EventLogQueue(
        Func<IReadOnlyList<EventLogEntryTvp>, CancellationToken, ValueTask> flushCallback,
        int capacity = DefaultCapacity,
        int batchSize = DefaultBatchSize,
        TimeSpan? interval = null,
        Action<Exception, int>? onFlushError = null,
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
            FullMode = BoundedChannelFullMode.Wait
        });
    }

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

    public bool Enqueue(EventLogEntryTvp entry)
    {
        if (_channel.Writer.TryWrite(entry))
            return true;

        _onDropped?.Invoke(1);
        return false;
    }

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
                    continue;

                var batch = new List<EventLogEntryTvp>(Math.Min(reader.Count, _batchSize));
                while (batch.Count < _batchSize && reader.TryRead(out var item))
                    batch.Add(item);

                if (batch.Count > 0)
                    await FlushBatchAsync(batch, loopCt).ConfigureAwait(false);
            }

            await DrainRemainingAsync(reader, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _loopExited.TrySetResult();
        }
    }

    private async ValueTask DrainRemainingAsync(ChannelReader<EventLogEntryTvp> reader, CancellationToken flushCt)
    {
        while (reader.TryRead(out var first))
        {
            var batch = new List<EventLogEntryTvp>(_batchSize) { first };
            while (batch.Count < _batchSize && reader.TryRead(out var item))
                batch.Add(item);

            await FlushBatchAsync(batch, flushCt).ConfigureAwait(false);
        }
    }

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
                _onFlushError?.Invoke(ex, batch.Count);
            }
            catch
            {
            }
        }
    }
}
