using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Hosting.Relay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class PartyResyncRelayHost(
    IEnumerable<IPartyResyncRelayHandler> handlers,
    IPartyResyncRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<PartyResyncRelayHost> logger)
    : AcknowledgedClusterRelayPumpBase<PartyResyncRelayEntry, PartyResyncRelayDto>(
            relay,
            options.Value.ShardId,
            QueueCapacity,
            TimeSpan.FromSeconds(options.Value.PartyResyncRelayPollIntervalSeconds),
            options.Value.PartyResyncRelayRetentionSeconds),
        IPartyResyncRelayQueue
{
    private const int QueueCapacity = 1024;

    private readonly IReadOnlyList<IPartyResyncRelayHandler> _handlers = handlers.ToArray();

    protected override long GetRelayId(PartyResyncRelayDto dto)
    {
        return dto.RelayId;
    }

    protected override async ValueTask DeliverAsync(PartyResyncRelayDto dto, CancellationToken ct)
    {
        if (_handlers.Count == 0)
        {
            logger.LogError(
                "Relayed party-resync row {RelayId} (sort {Sort}, party {PartyName}) has no registered " +
                "IPartyResyncRelayHandler in this composition; it remains unacknowledged",
                dto.RelayId, dto.Sort, dto.PartyName);
            throw new InvalidOperationException("No party-resync relay handler is registered.");
        }

        foreach (var handler in _handlers)
            await handler.HandleAsync(dto, ct).ConfigureAwait(false);
    }

    protected override void OnOutboxFull(PartyResyncRelayEntry entry)
    {
        logger.LogWarning(
            "Cross-shard party-resync relay outbox full on shard {ShardId}; dropping one sort-{Sort} row for " +
            "party {PartyName} (character {SourceCharacterId}) -- a missed resync only leaves the reconnecting " +
            "client's party UI unchanged, no durable state is lost",
            options.Value.ShardId, entry.Sort, entry.PartyName, entry.SourceCharacterId);
    }

    protected override void OnOutboundFlushFailed(Exception ex)
    {
        logger.LogError(ex, "Party-resync relay outbound flush failed for shard {ShardId}", options.Value.ShardId);
    }

    protected override void OnInboundDeliveryFailed(Exception ex)
    {
        logger.LogError(ex, "Party-resync relay inbound delivery failed for shard {ShardId}", options.Value.ShardId);
    }

    protected override void OnPublishFailed(PartyResyncRelayEntry entry, Exception ex)
    {
        logger.LogError(ex,
            "Failed to publish a sort-{Sort} party-resync row for party {PartyName} (character " +
            "{SourceCharacterId}) from shard {ShardId}; this one resync is lost",
            entry.Sort, entry.PartyName, entry.SourceCharacterId, options.Value.ShardId);
    }

    protected override void OnDeliveryOrAcknowledgementFailed(PartyResyncRelayDto dto, Exception ex)
    {
        logger.LogError(ex,
            "Failed to reconcile or acknowledge relayed party-resync row {RelayId} (sort {Sort}, party " +
            "{PartyName}) on shard {ShardId}; the cursor was not advanced",
            dto.RelayId, dto.Sort, dto.PartyName, options.Value.ShardId);
    }
}
