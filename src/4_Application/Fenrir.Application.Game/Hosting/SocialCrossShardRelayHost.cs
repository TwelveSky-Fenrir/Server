using System.Collections.Frozen;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class SocialCrossShardRelayHost(
    IEnumerable<ISocialCrossShardRelayHandler> handlers,
    ISocialCrossShardRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<SocialCrossShardRelayHost> logger) : BackgroundService, ISocialCrossShardRelayQueue
{
    private const int QueueCapacity = 1024;

    private readonly FrozenDictionary<SocialCrossShardRelayKind, ISocialCrossShardRelayHandler> _handlersByKind =
        handlers.ToFrozenDictionary(static h => h.Kind);

    private readonly Channel<SocialCrossShardRelayEntry> _outbox =
        Channel.CreateBounded<SocialCrossShardRelayEntry>(
            new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

    public bool Enqueue(SocialCrossShardRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard social relay outbox full on shard {ShardId}; dropping one {Kind}/{MessageType} row " +
            "addressed to character {TargetCharacterId} on shard {TargetShardId} (any same-shard delivery " +
            "already happened, only the cross-shard leg of this one step is lost)",
            options.Value.ShardId, entry.Kind, entry.MessageType, entry.TargetCharacterId, entry.TargetShardId);
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
                logger.LogError(ex, "Social cross-shard relay outbound flush failed for shard {ShardId}",
                    options.Value.ShardId);
            }
    }

    private async Task RunInboundDeliveryLoopAsync(CancellationToken stoppingToken)
    {
        using var timer =
            new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SocialCrossShardRelayPollIntervalSeconds));

        do
        {
            try
            {
                await DeliverInboundAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Social cross-shard relay inbound delivery failed for shard {ShardId}",
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
                    "Failed to publish a {Kind}/{MessageType} row to the cross-shard social relay from shard " +
                    "{ShardId} (target character {TargetCharacterId} on shard {TargetShardId}); this one step " +
                    "is lost",
                    entry.Kind, entry.MessageType, options.Value.ShardId, entry.TargetCharacterId,
                    entry.TargetShardId);
            }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.SocialCrossShardRelayRetentionSeconds;

        var incoming = await relay.PollAsync(shardId, retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                await CrossShardRelayRetry.RunAsync(() => DeliverLocallyAsync(dto, ct), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to locally deliver relayed social row {RelayId} (kind {Kind}, message {MessageType}) " +
                    "on shard {ShardId}",
                    dto.RelayId, dto.Kind, dto.MessageType, shardId);
            }
    }

    private async ValueTask DeliverLocallyAsync(SocialCrossShardRelayDto dto, CancellationToken ct)
    {
        if (!_handlersByKind.TryGetValue((SocialCrossShardRelayKind)dto.Kind, out var handler))
        {
            logger.LogWarning(
                "Relayed social row {RelayId} has Kind {Kind} with no registered ISocialCrossShardRelayHandler; dropped",
                dto.RelayId, dto.Kind);
            return;
        }

        switch ((SocialCrossShardRelayMessageType)dto.MessageType)
        {
            case SocialCrossShardRelayMessageType.Ask:
                await handler.HandleAskAsync(dto, ct).ConfigureAwait(false);
                break;

            case SocialCrossShardRelayMessageType.Answer:
                await handler.HandleAnswerAsync(dto, ct).ConfigureAwait(false);
                break;

            default:
                logger.LogWarning("Relayed social row {RelayId} has unrecognized MessageType {MessageType}; dropped",
                    dto.RelayId, dto.MessageType);
                break;
        }
    }
}
