using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class PartyResyncRelayHost(
    IEnumerable<IPartyResyncRelayHandler> handlers,
    IPartyResyncRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<PartyResyncRelayHost> logger) : BackgroundService, IPartyResyncRelayQueue
{
    private const int QueueCapacity = 1024;
    private const int MaxDrainedPerCycle = 512;

    private readonly IReadOnlyList<IPartyResyncRelayHandler> _handlers = handlers.ToArray();

    private readonly Channel<PartyResyncRelayEntry> _outbox =
        Channel.CreateBounded<PartyResyncRelayEntry>(
            new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

    public bool Enqueue(PartyResyncRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard party-resync relay outbox full on shard {ShardId}; dropping one sort-{Sort} row for " +
            "party {PartyName} (character {SourceCharacterId}) -- a missed resync only leaves the reconnecting " +
            "client's party UI unchanged, no durable state is lost",
            options.Value.ShardId, entry.Sort, entry.PartyName, entry.SourceCharacterId);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer =
            new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PartyResyncRelayPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Party-resync relay poll failed for shard {ShardId}", options.Value.ShardId);
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
        var drained = 0;

        while (drained < MaxDrainedPerCycle && reader.TryRead(out var entry))
        {
            drained++;
            try
            {
                await relay.PublishAsync(entry, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to publish a sort-{Sort} party-resync row for party {PartyName} (character " +
                    "{SourceCharacterId}) from shard {ShardId}; this one resync is lost",
                    entry.Sort, entry.PartyName, entry.SourceCharacterId, options.Value.ShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.PartyResyncRelayRetentionSeconds;

        var incoming = await relay.PollAsync(shardId, retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                await DeliverLocallyAsync(dto, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to reconcile relayed party-resync row {RelayId} (sort {Sort}, party {PartyName}) on " +
                    "shard {ShardId}",
                    dto.RelayId, dto.Sort, dto.PartyName, shardId);
            }
    }

    private async ValueTask DeliverLocallyAsync(PartyResyncRelayDto dto, CancellationToken ct)
    {
        if (_handlers.Count == 0)
        {
            logger.LogWarning(
                "Relayed party-resync row {RelayId} (sort {Sort}, party {PartyName}) has no registered " +
                "IPartyResyncRelayHandler in this composition; dropped -- same-shard party membership is unaffected",
                dto.RelayId, dto.Sort, dto.PartyName);
            return;
        }

        foreach (var handler in _handlers)
            await handler.HandleAsync(dto, ct).ConfigureAwait(false);
    }
}
