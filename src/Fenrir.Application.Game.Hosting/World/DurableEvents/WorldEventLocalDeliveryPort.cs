namespace Fenrir.Application.Game.Hosting.World.DurableEvents;

public enum WorldEventDeliveryGuarantee : byte
{
    AtLeastOnce = 1
}

public readonly record struct WorldEventDeliveryContext(
    long OutboxId,
    string AuthenticatedSource,
    byte SourceShardId,
    long SourceSequence,
    byte DestinationShardId,
    Guid CorrelationId,
    Guid IdempotencyKey,
    short DeliveryAttempt,
    WorldEventDeliveryGuarantee Guarantee);

public readonly record struct WorldStateWorldEvent(WorldEventDeliveryContext Context, ReadOnlyMemory<byte> Payload);

public readonly record struct ZoneWarWorldEvent(WorldEventDeliveryContext Context, ReadOnlyMemory<byte> Payload);

public readonly record struct WorldNoticeWorldEvent(WorldEventDeliveryContext Context, ReadOnlyMemory<byte> Payload);

public readonly record struct CrossShardSocialWorldEvent(WorldEventDeliveryContext Context,
    ReadOnlyMemory<byte> Payload);

public readonly record struct EconomyWorldEvent(WorldEventDeliveryContext Context, ReadOnlyMemory<byte> Payload);

public readonly record struct AdministrationWorldEvent(WorldEventDeliveryContext Context,
    ReadOnlyMemory<byte> Payload);

public readonly record struct WorldEventLocalEffectCompletion(Guid OperationKey)
{
    public static WorldEventLocalEffectCompletion For(Guid operationKey)
    {
        if (operationKey == Guid.Empty)
            throw new ArgumentException("A completed world effect requires an operation key.", nameof(operationKey));

        return new WorldEventLocalEffectCompletion(operationKey);
    }
}

public interface IWorldEventLocalDeliveryPort
{
    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(WorldStateWorldEvent worldEvent, CancellationToken ct);

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(ZoneWarWorldEvent worldEvent, CancellationToken ct);

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(WorldNoticeWorldEvent worldEvent, CancellationToken ct);

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(CrossShardSocialWorldEvent worldEvent,
        CancellationToken ct);

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(EconomyWorldEvent worldEvent, CancellationToken ct);

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(AdministrationWorldEvent worldEvent,
        CancellationToken ct);
}

public sealed class UnconfiguredWorldEventLocalDeliveryPort : IWorldEventLocalDeliveryPort
{
    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(WorldStateWorldEvent worldEvent, CancellationToken ct)
    {
        return MissingBindingAsync();
    }

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(ZoneWarWorldEvent worldEvent, CancellationToken ct)
    {
        return MissingBindingAsync();
    }

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(WorldNoticeWorldEvent worldEvent, CancellationToken ct)
    {
        return MissingBindingAsync();
    }

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(CrossShardSocialWorldEvent worldEvent,
        CancellationToken ct)
    {
        return MissingBindingAsync();
    }

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(EconomyWorldEvent worldEvent, CancellationToken ct)
    {
        return MissingBindingAsync();
    }

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(AdministrationWorldEvent worldEvent,
        CancellationToken ct)
    {
        return MissingBindingAsync();
    }

    private static ValueTask<WorldEventLocalEffectCompletion> MissingBindingAsync()
    {
        return ValueTask.FromException<WorldEventLocalEffectCompletion>(new InvalidOperationException(
            "No IWorldEventLocalDeliveryPort implementation is configured for durable world-event delivery."));
    }
}
