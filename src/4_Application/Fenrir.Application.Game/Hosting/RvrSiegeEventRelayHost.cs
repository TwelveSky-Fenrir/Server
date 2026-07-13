using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class RvrSiegeEventRelayHost(
    Lazy<ZoneCenterBroadcastIngestor> ingestor,
    Lazy<ZoneEventBroadcaster> broadcaster,
    IRvrSiegeEventRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<RvrSiegeEventRelayHost> logger) : BackgroundService, IRvrSiegeEventRelayQueue
{
    private const int Zone049RangeStart = 1;

    private const int Zone049RangeEnd = 9;

    private const int QueueCapacity = 256;

    private readonly Channel<RvrSiegeEventRelayEntry> _outbox = Channel.CreateBounded<RvrSiegeEventRelayEntry>(
        new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public bool Enqueue(RvrSiegeEventRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard rvr-siege relay outbox full on shard {ShardId}; dropping the cross-shard fan-out for " +
            "sort {Sort} (same-shard delivery already happened, only the cross-shard leg is lost)",
            options.Value.ShardId, entry.Sort);
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
                logger.LogError(ex, "RvR-siege relay outbound flush failed for shard {ShardId}",
                    options.Value.ShardId);
            }
    }

    private async Task RunInboundDeliveryLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.RvrSiegeEventRelayPollIntervalSeconds));

        do
        {
            try
            {
                await DeliverInboundAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "RvR-siege relay inbound delivery failed for shard {ShardId}",
                    options.Value.ShardId);
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
                await CrossShardRelayRetry.RunAsync(() => relay.PublishAsync(entry, ct), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to publish an rvr-siege relay row (sort {Sort}) from shard {ShardId}; cross-shard " +
                    "fan-out for this one event is lost (same-shard delivery already happened)",
                    entry.Sort, options.Value.ShardId);
            }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.RvrSiegeEventRelayRetentionSeconds;

        var incoming = await relay.PollAsync(shardId, retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                await CrossShardRelayRetry.RunSync(() => DeliverLocally(dto), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to locally deliver relayed rvr-siege event {RelayId} (sort {Sort}) on shard {ShardId}",
                    dto.RelayId, dto.Sort, shardId);
            }
    }

    private void DeliverLocally(RvrSiegeEventRelayDto dto)
    {
        if (dto.Sort is >= Zone049RangeStart and <= Zone049RangeEnd)
            ingestor.Value.ApplyRelayedEvent(dto.Sort, dto.Data);
        else
            broadcaster.Value.ApplyRelayedEvent(dto.Sort, dto.Data);
    }
}
