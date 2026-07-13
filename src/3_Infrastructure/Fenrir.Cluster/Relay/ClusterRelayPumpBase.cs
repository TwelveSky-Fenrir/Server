using System.Threading.Channels;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Cluster.Relay;

public abstract class ClusterRelayPumpBase<TEntry, TDto> : BackgroundService
{
    private readonly IClusterRelayBackend<TEntry, TDto> _backend;
    private readonly byte _shardId;
    private readonly TimeSpan _pollInterval;
    private readonly int _retentionSeconds;
    private readonly Channel<TEntry> _outbox;

    protected ClusterRelayPumpBase(
        IClusterRelayBackend<TEntry, TDto> backend,
        byte shardId,
        int capacity,
        TimeSpan pollInterval,
        int retentionSeconds)
    {
        _backend = backend;
        _shardId = shardId;
        _pollInterval = pollInterval;
        _retentionSeconds = retentionSeconds;
        _outbox = Channel.CreateBounded<TEntry>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

        public bool Enqueue(TEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        OnOutboxFull(entry);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var outboundLoop = RunOutboundFlushLoopAsync(stoppingToken);
        var inboundLoop = RunInboundDeliveryLoopAsync(stoppingToken);
        await Task.WhenAll(outboundLoop, inboundLoop).ConfigureAwait(false);
    }

    private async Task RunOutboundFlushLoopAsync(CancellationToken stoppingToken)
    {
        var reader = _outbox.Reader;

        while (await reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            try
            {
                await FlushOutboundAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnOutboundFlushFailed(ex);
            }
    }

    private async Task RunInboundDeliveryLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);

        do
        {
            try
            {
                await DeliverInboundAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnInboundDeliveryFailed(ex);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

        public async ValueTask PollOnceAsync(CancellationToken ct)
    {
        await FlushOutboundAsync(ct).ConfigureAwait(false);
        await DeliverInboundAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask FlushOutboundAsync(CancellationToken ct)
    {
        var reader = _outbox.Reader;

        while (reader.TryRead(out var entry))
            try
            {
                await CrossShardRelayRetry.RunAsync(() => _backend.PublishAsync(entry, ct), ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnPublishFailed(entry, ex);
            }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var incoming = await _backend.PollAsync(_shardId, _retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                await CrossShardRelayRetry.RunAsync(() => DeliverAsync(dto, ct), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnDeliveryFailed(dto, ex);
            }
    }

        protected abstract ValueTask DeliverAsync(TDto dto, CancellationToken ct);

        protected abstract void OnOutboxFull(TEntry entry);

        protected abstract void OnOutboundFlushFailed(Exception ex);

        protected abstract void OnInboundDeliveryFailed(Exception ex);

        protected abstract void OnPublishFailed(TEntry entry, Exception ex);

        protected abstract void OnDeliveryFailed(TDto dto, Exception ex);
}
