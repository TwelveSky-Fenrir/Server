using System.Security.Cryptography;
using Fenrir.Application.Game.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.DurableEvents;

public sealed class WorldOutboxDispatcherHost(
    IWorldEventOutboxRepository outbox,
    IWorldEventInboxRepository inbox,
    IWorldEventLocalDeliveryPort localDelivery,
    IOptions<GameServerOptions> options,
    ILogger<WorldOutboxDispatcherHost> logger) : BackgroundService
{
    public const int MaximumBatchSize = 64;

    public const int DeliveryLeaseSeconds = 30;

    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly byte _shardId = options.Value.ShardId;
    private Guid _deliveryLeaseId = Guid.NewGuid();

    public async ValueTask DispatchOnceAsync(CancellationToken ct)
    {
        var deliveryLeaseId = _deliveryLeaseId;
        var deliveries = await outbox.ReadAsync(
                new WorldEventOutboxReadRequest(_shardId, deliveryLeaseId, MaximumBatchSize, DeliveryLeaseSeconds), ct)
            .ConfigureAwait(false);

        if (deliveries.IsEmpty)
        {
            _deliveryLeaseId = Guid.NewGuid();
            return;
        }

        foreach (var delivery in deliveries)
            await DeliverAndAcknowledgeAsync(delivery, deliveryLeaseId, ct).ConfigureAwait(false);

        _deliveryLeaseId = Guid.NewGuid();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Durable world-event dispatch on shard {ShardId} uses {Guarantee}; a process failure after local delivery and before acknowledgement can replay an event",
            _shardId, WorldEventDeliveryGuarantee.AtLeastOnce);

        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await DispatchOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Durable world-event delivery failed on shard {ShardId}; the current lease remains unacknowledged and is eligible for at-least-once retry",
                    _shardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async ValueTask DeliverAndAcknowledgeAsync(WorldEventOutboxDeliveryDto delivery, Guid deliveryLeaseId,
        CancellationToken ct)
    {
        var category = Validate(delivery);
        var context = new WorldEventDeliveryContext(
            delivery.OutboxId,
            delivery.AuthenticatedSource,
            delivery.SourceShardId,
            delivery.SourceSequence,
            delivery.DestinationShardId,
            delivery.CorrelationId,
            delivery.IdempotencyKey,
            delivery.AttemptCount,
            WorldEventDeliveryGuarantee.AtLeastOnce);
        var payload = new ReadOnlyMemory<byte>(delivery.Payload.ToArray());

        var receipt = await inbox.ReceiptAsync(
                new WorldEventInboxReceiptRequest(delivery.OutboxId, _shardId, deliveryLeaseId), ct)
            .ConfigureAwait(false);

        if (!receipt.IsEffectCompleted)
        {
            var completion = await DeliverLocallyAsync(category, context, payload, ct).ConfigureAwait(false);
            if (completion.OperationKey != context.IdempotencyKey)
                throw new InvalidOperationException(
                    $"World inbox delivery {delivery.OutboxId} completed with a mismatched operation key.");
        }

        var acknowledged = await inbox.AcknowledgeAsync(
                new WorldEventInboxAcknowledgement(delivery.OutboxId, _shardId, deliveryLeaseId), ct)
            .ConfigureAwait(false);

        if (!acknowledged)
            throw new InvalidOperationException(
                $"World inbox delivery {delivery.OutboxId} completed locally but could not be acknowledged.");
    }

    private ValueTask<WorldEventLocalEffectCompletion> DeliverLocallyAsync(WorldEventPayloadCategory category,
        WorldEventDeliveryContext context,
        ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        return category switch
        {
            WorldEventPayloadCategory.WorldState => localDelivery.DeliverAsync(
                new WorldStateWorldEvent(context, payload),
                ct),
            WorldEventPayloadCategory.ZoneWar =>
                localDelivery.DeliverAsync(new ZoneWarWorldEvent(context, payload), ct),
            WorldEventPayloadCategory.WorldNotice => localDelivery.DeliverAsync(
                new WorldNoticeWorldEvent(context, payload), ct),
            WorldEventPayloadCategory.CrossShardSocial => localDelivery.DeliverAsync(
                new CrossShardSocialWorldEvent(context, payload), ct),
            WorldEventPayloadCategory.Economy =>
                localDelivery.DeliverAsync(new EconomyWorldEvent(context, payload), ct),
            WorldEventPayloadCategory.Administration => localDelivery.DeliverAsync(
                new AdministrationWorldEvent(context, payload), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category,
                "The world-event payload category is not part of the fixed delivery contract.")
        };
    }

    private WorldEventPayloadCategory Validate(WorldEventOutboxDeliveryDto delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        if (delivery.OutboxId <= 0)
            throw new InvalidOperationException("World outbox delivery requires a positive identifier.");
        if (delivery.AuthenticatedSource is not { Length: > 0 and <= 128 })
            throw new InvalidOperationException("World outbox delivery has an invalid authenticated source.");
        if (delivery.SourceSequence <= 0)
            throw new InvalidOperationException("World outbox delivery requires a positive source sequence.");
        if (delivery.SourceShardId == delivery.DestinationShardId)
            throw new InvalidOperationException("World outbox delivery cannot target its source shard.");
        if (delivery.DestinationShardId != _shardId)
            throw new InvalidOperationException("World outbox delivery was leased for a different destination shard.");
        if (!Enum.IsDefined((WorldEventPayloadCategory)delivery.PayloadCategory))
            throw new InvalidOperationException("World outbox delivery has an unknown payload category.");
        if (delivery.Payload is not { Length: > 0 and <= WorldEventOutboxLimits.MaximumPayloadBytes })
            throw new InvalidOperationException("World outbox delivery has an invalid payload length.");
        if (delivery.PayloadHash is not { Length: SHA256.HashSizeInBytes })
            throw new InvalidOperationException("World outbox delivery has an invalid payload hash.");
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(delivery.Payload), delivery.PayloadHash))
            throw new InvalidOperationException("World outbox delivery payload hash validation failed.");
        if (delivery.CorrelationId == Guid.Empty || delivery.IdempotencyKey == Guid.Empty)
            throw new InvalidOperationException(
                "World outbox delivery requires correlation and idempotency identifiers.");
        if (delivery.AttemptCount is < 1 or > WorldEventOutboxLimits.MaximumDeliveryAttempts)
            throw new InvalidOperationException("World outbox delivery has an invalid attempt count.");

        return (WorldEventPayloadCategory)delivery.PayloadCategory;
    }
}
