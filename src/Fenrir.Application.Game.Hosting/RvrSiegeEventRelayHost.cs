using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.Relay;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class RvrSiegeEventRelayHost(
    Lazy<ZoneCenterBroadcastIngestor> ingestor,
    IRvrSiegeEventRelayRepository relay,
    IZoneEventRelayOutboxRepository outbox,
    IZoneEventRelayOutboxWakeSignal wakeSignal,
    IOptions<GameServerOptions> options,
    ILogger<RvrSiegeEventRelayHost> logger) : BackgroundService
{
    public const int MaximumOutboundBatchSize = 32;
    public const int OutboundLeaseSeconds = 30;
    private static readonly TimeSpan OutboundRetryInterval = TimeSpan.FromSeconds(1);

    public async ValueTask PollOnceAsync(CancellationToken ct)
    {
        await PublishOutboundOnceAsync(ct).ConfigureAwait(false);
        await DeliverInboundOnceAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask PublishOutboundOnceAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var leaseId = Guid.NewGuid();
        var entries = await outbox.ClaimAsync(
                new ZoneEventRelayOutboxClaimRequest(shardId, leaseId, MaximumOutboundBatchSize,
                    OutboundLeaseSeconds), ct)
            .ConfigureAwait(false);

        foreach (var entry in entries)
        {
            try
            {
                ValidateOutboundEntry(entry, shardId);
                await CrossShardRelayRetry.RunAsync(
                        () => relay.PublishAsync(new RvrSiegeEventRelayEntry(entry.SourceShardId, entry.Sort,
                        entry.Data) { CorrelationId = entry.CorrelationId }, ct), ct)
                    .ConfigureAwait(false);

                var acknowledged = await outbox.AcknowledgeAsync(
                        new ZoneEventRelayOutboxAcknowledgement(entry.OutboxId, shardId, leaseId), ct)
                    .ConfigureAwait(false);
                if (!acknowledged)
                    throw new InvalidOperationException(
                        $"Zone-event relay outbox entry {entry.OutboxId} was published but could not be acknowledged.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Zone-event relay outbox entry {OutboxId} ({OperationId}, correlation {CorrelationId}) was not fully published from shard {ShardId}; it remains durable for idempotent retry",
                    entry.OutboxId, entry.OperationId, entry.CorrelationId, shardId);
                break;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var outbound = RunOutboundLoopAsync(stoppingToken);
        var inbound = RunInboundLoopAsync(stoppingToken);
        await Task.WhenAll(outbound, inbound).ConfigureAwait(false);
    }

    private async Task RunOutboundLoopAsync(CancellationToken stoppingToken)
    {
        do
        {
            try
            {
                await PublishOutboundOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "RvR-siege durable relay publication failed on shard {ShardId}; unacknowledged work remains retryable",
                    options.Value.ShardId);
            }

            using var signalCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var signal = wakeSignal.WaitAsync(signalCancellation.Token).AsTask();
            var retry = Task.Delay(OutboundRetryInterval, stoppingToken);
            if (await Task.WhenAny(signal, retry).ConfigureAwait(false) != signal)
            {
                await signalCancellation.CancelAsync().ConfigureAwait(false);
                try
                {
                    await signal.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                }
            }
            stoppingToken.ThrowIfCancellationRequested();
        } while (true);
    }

    private async Task RunInboundLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.RvrSiegeEventRelayPollIntervalSeconds));

        do
        {
            try
            {
                await DeliverInboundOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "RvR-siege durable relay delivery failed on shard {ShardId}; the inbound cursor was not advanced",
                    options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async ValueTask DeliverInboundOnceAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var incoming = await relay.PollAsync(shardId, options.Value.RvrSiegeEventRelayRetentionSeconds, ct)
            .ConfigureAwait(false);

        foreach (var dto in incoming)
            try
            {
                await DeliverAsync(dto, ct).ConfigureAwait(false);
                await CrossShardRelayRetry.RunAsync(() => relay.AcknowledgeAsync(shardId, dto.RelayId, ct), ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to deliver or acknowledge relayed rvr-siege event {RelayId} (sort {Sort}) on shard {ShardId}; the cursor was not advanced",
                    dto.RelayId, dto.Sort, shardId);
                break;
            }
    }

    private ValueTask DeliverAsync(RvrSiegeEventRelayDto dto, CancellationToken ct)
    {
        if (!KnownTSortRegistry.IsKnown(dto.Sort))
        {
            logger.LogWarning(
                "Cross-shard rvr-siege relay {RelayId} sort {Sort} rejected: not in the known-sort allowlist",
                dto.RelayId, dto.Sort);
            return ValueTask.CompletedTask;
        }

        ingestor.Value.ApplyRelayedEvent(dto.Sort, dto.Data);
        return ValueTask.CompletedTask;
    }

    private static void ValidateOutboundEntry(ZoneEventRelayOutboxDeliveryDto entry, byte shardId)
    {
        if (entry.OutboxId <= 0 || entry.SourceShardId != shardId || entry.Data is not { Length: 130 } ||
            entry.OperationId == Guid.Empty || entry.CorrelationId == Guid.Empty || entry.AttemptCount <= 0 ||
            !KnownTSortRegistry.IsKnown(entry.Sort))
            throw new InvalidOperationException("The durable zone-event relay entry is invalid.");
    }
}
