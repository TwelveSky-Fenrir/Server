using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Hosting.World.DurableEvents;

public sealed class WorldStateWorldEventLocalDeliveryPort(
    IWorldEventLocalEffectRepository effects,
    WorldStateService worldState) : IWorldEventLocalDeliveryPort
{
    public async ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(WorldStateWorldEvent worldEvent,
        CancellationToken ct)
    {
        await effects.ApplyWorldStateHighTribeAsync(
                new WorldStateInboundEffectRequest(
                    worldEvent.Context.OutboxId,
                    worldEvent.Context.DestinationShardId,
                    worldEvent.Context.IdempotencyKey,
                    worldEvent.Payload.ToArray()),
                ct)
            .ConfigureAwait(false);

        if (!await worldState.ReconcileAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException(
                "The world-state effect committed but the local projection did not reconcile.");

        return WorldEventLocalEffectCompletion.For(worldEvent.Context.IdempotencyKey);
    }

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(ZoneWarWorldEvent worldEvent, CancellationToken ct)
    {
        return UnsupportedAsync(WorldEventPayloadCategory.ZoneWar);
    }

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(WorldNoticeWorldEvent worldEvent,
        CancellationToken ct)
    {
        return UnsupportedAsync(WorldEventPayloadCategory.WorldNotice);
    }

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(CrossShardSocialWorldEvent worldEvent,
        CancellationToken ct)
    {
        return UnsupportedAsync(WorldEventPayloadCategory.CrossShardSocial);
    }

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(EconomyWorldEvent worldEvent, CancellationToken ct)
    {
        return UnsupportedAsync(WorldEventPayloadCategory.Economy);
    }

    public ValueTask<WorldEventLocalEffectCompletion> DeliverAsync(AdministrationWorldEvent worldEvent,
        CancellationToken ct)
    {
        return UnsupportedAsync(WorldEventPayloadCategory.Administration);
    }

    private static ValueTask<WorldEventLocalEffectCompletion> UnsupportedAsync(WorldEventPayloadCategory category)
    {
        return ValueTask.FromException<WorldEventLocalEffectCompletion>(new NotSupportedException(
            $"Durable local delivery is not configured for {category}."));
    }
}
