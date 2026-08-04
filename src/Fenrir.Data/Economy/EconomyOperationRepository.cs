using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Economy;

namespace Fenrir.Data.Economy;

public sealed record EconomyOperationRepository(ICaeriusNetDbContext Db) : IEconomyOperationRepository
{
    public async ValueTask<EconomyOperationBeginResult> BeginOrReadAsync(
        int actorAccountId,
        int? actorCharacterId,
        EconomyOperationKind operationKind,
        EconomyOperationCause cause,
        EconomyOperationIdempotencyKeyHash idempotencyKeyHash,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(actorAccountId);

        if (actorCharacterId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(actorCharacterId));

        ArgumentNullException.ThrowIfNull(idempotencyKeyHash);
        EnsureDefined(operationKind, nameof(operationKind));
        EnsureDefined(cause, nameof(cause));

        var sp = new StoredProcedureParametersBuilder("game", "usp_EconomyOperation_BeginOrRead", 1)
            .AddParameter("ActorAccountId", actorAccountId, SqlDbType.Int)
            .AddParameter("ActorCharacterId", (object?)actorCharacterId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("OperationKind", (byte)operationKind, SqlDbType.TinyInt)
            .AddParameter("Cause", (byte)cause, SqlDbType.TinyInt)
            .AddParameter("IdempotencyKeyHash", idempotencyKeyHash.ToArray(), SqlDbType.Binary)
            .Build();

        return await Db.FirstQueryAsync<EconomyOperationBeginResult>(sp, ct) ??
               throw new InvalidOperationException(
                   "usp_EconomyOperation_BeginOrRead always returns exactly one operation result row.");
    }

    public async ValueTask<EconomyOperationCompleteResult> CompleteAsync(Guid operationId, int actorAccountId,
        EconomyOperationStatus finalStatus, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(actorAccountId);

        if (operationId == Guid.Empty)
            throw new ArgumentException("An economy operation identifier is required.", nameof(operationId));

        if (finalStatus is EconomyOperationStatus.Pending || !Enum.IsDefined(finalStatus))
            throw new ArgumentOutOfRangeException(nameof(finalStatus));

        var sp = new StoredProcedureParametersBuilder("game", "usp_EconomyOperation_Complete", 1)
            .AddParameter("OperationId", operationId, SqlDbType.UniqueIdentifier)
            .AddParameter("ActorAccountId", actorAccountId, SqlDbType.Int)
            .AddParameter("FinalStatus", (byte)finalStatus, SqlDbType.TinyInt)
            .Build();

        return await Db.FirstQueryAsync<EconomyOperationCompleteResult>(sp, ct) ??
               throw new InvalidOperationException(
                   "usp_EconomyOperation_Complete always returns exactly one operation result row.");
    }

    private static void EnsureDefined<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
