namespace Fenrir.Data.Abstractions.Economy;

public interface IEconomyOperationRepository
{
    public ValueTask<EconomyOperationBeginResult> BeginOrReadAsync(
        int actorAccountId,
        int? actorCharacterId,
        EconomyOperationKind operationKind,
        EconomyOperationCause cause,
        EconomyOperationIdempotencyKeyHash idempotencyKeyHash,
        CancellationToken ct);

    public ValueTask<EconomyOperationCompleteResult> CompleteAsync(
        Guid operationId,
        int actorAccountId,
        EconomyOperationStatus finalStatus,
        CancellationToken ct);
}
