using System.Threading.Channels;
using Fenrir.Data.Abstractions.Game;

namespace Fenrir.Data.WriteBehind;

public sealed class EventLogQueue(
    Func<IReadOnlyList<EventLogEntryTvp>, CancellationToken, ValueTask> flushCallback,
    int capacity = EventLogQueue.DefaultCapacity,
    int batchSize = EventLogQueue.DefaultBatchSize,
    TimeSpan? interval = null,
    Action<Exception, int>? onFlushError = null,
    Action<int>? onDropped = null)
    : IEventLogQueue, IAsyncDisposable
{
    public const int DefaultCapacity = 4096;
    public const int DefaultBatchSize = 256;
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

    private readonly Channel<EventLogEntryTvp> _channel = Channel.CreateBounded<EventLogEntryTvp>(new BoundedChannelOptions(capacity)
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.Wait
    });

    private readonly TimeSpan _interval = interval ?? DefaultInterval;
    private readonly TaskCompletionSource _loopExited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly CancellationTokenSource _shutdownCts = new();
    private int _disposed;
    private int _runStarted;

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

        onDropped?.Invoke(1);
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

                var batch = new List<EventLogEntryTvp>(Math.Min(reader.Count, batchSize));
                while (batch.Count < batchSize && reader.TryRead(out var item))
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
            var batch = new List<EventLogEntryTvp>(batchSize) { first };
            while (batch.Count < batchSize && reader.TryRead(out var item))
                batch.Add(item);

            await FlushBatchAsync(batch, flushCt).ConfigureAwait(false);
        }
    }

    private async ValueTask FlushBatchAsync(IReadOnlyList<EventLogEntryTvp> batch, CancellationToken loopCt)
    {
        try
        {
            await flushCallback(batch, loopCt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                onFlushError?.Invoke(ex, batch.Count);
            }
            catch
            {
            }
        }
    }
}
