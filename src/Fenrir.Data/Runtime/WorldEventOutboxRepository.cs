using System.Collections.Immutable;
using System.Data;
using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Exceptions;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record WorldEventOutboxRepository(ICaeriusNetDbContext Db) : IWorldEventOutboxRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorDeadlockVictim = 1205;
    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;
    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask<WorldEventOutboxEnqueueResultDto> EnqueueAsync(WorldEventOutboxEntry entry,
        CancellationToken ct)
    {
        ValidateEntry(entry);
        var payloadHash = SHA256.HashData(entry.Payload);

        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_WorldOutbox_Enqueue", 1,
                    CommandTimeoutSeconds)
                .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
                .AddParameter("SourceSequence", entry.SourceSequence, SqlDbType.BigInt)
                .AddParameter("DestinationShardId", entry.DestinationShardId, SqlDbType.TinyInt)
                .AddParameter("PayloadCategory", (byte)entry.PayloadCategory, SqlDbType.TinyInt)
                .AddParameter("Payload", entry.Payload, SqlDbType.VarBinary, WorldEventOutboxLimits.MaximumPayloadBytes)
                .AddParameter("PayloadHash", payloadHash, SqlDbType.Binary, SHA256.HashSizeInBytes)
                .AddParameter("CorrelationId", entry.CorrelationId, SqlDbType.UniqueIdentifier)
                .AddParameter("IdempotencyKey", entry.IdempotencyKey, SqlDbType.UniqueIdentifier)
                .Build();

            try
            {
                return await Db.FirstQueryAsync<WorldEventOutboxEnqueueResultDto>(sp, ct) ??
                       throw new InvalidOperationException(
                           "usp_WorldOutbox_Enqueue always returns the persistent outbox identifier.");
            }
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts && IsRetryableTransactionFailure(ex))
            {
            }
        }
    }

    public async ValueTask<ImmutableArray<WorldEventOutboxDeliveryDto>> ReadAsync(WorldEventOutboxReadRequest request,
        CancellationToken ct)
    {
        ValidateReadRequest(request);

        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_WorldOutbox_Read", request.MaximumCount,
                    CommandTimeoutSeconds)
                .AddParameter("DestinationShardId", request.DestinationShardId, SqlDbType.TinyInt)
                .AddParameter("DeliveryLeaseId", request.DeliveryLeaseId, SqlDbType.UniqueIdentifier)
                .AddParameter("MaximumCount", request.MaximumCount, SqlDbType.Int)
                .AddParameter("LeaseSeconds", request.LeaseSeconds, SqlDbType.Int)
                .Build();

            try
            {
                return await Db.QueryAsImmutableArrayAsync<WorldEventOutboxDeliveryDto>(sp, ct);
            }
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts && IsRetryableTransactionFailure(ex))
            {
            }
        }
    }

    private static void ValidateEntry(WorldEventOutboxEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.SourceSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(entry), "The source sequence must be positive.");
        if (entry.SourceShardId == entry.DestinationShardId)
            throw new ArgumentException("A world event cannot target its source shard.", nameof(entry));
        if (!Enum.IsDefined(entry.PayloadCategory))
            throw new ArgumentOutOfRangeException(nameof(entry), "The payload category is not part of the fixed contract.");
        if (entry.Payload is not { Length: > 0 and <= WorldEventOutboxLimits.MaximumPayloadBytes })
            throw new ArgumentOutOfRangeException(nameof(entry),
                $"Payloads must contain from 1 to {WorldEventOutboxLimits.MaximumPayloadBytes} bytes.");
        if (entry.CorrelationId == Guid.Empty)
            throw new ArgumentException("A correlation identifier is required.", nameof(entry));
        if (entry.IdempotencyKey == Guid.Empty)
            throw new ArgumentException("An idempotency key is required.", nameof(entry));
    }

    private static void ValidateReadRequest(WorldEventOutboxReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DeliveryLeaseId == Guid.Empty)
            throw new ArgumentException("An outbox read requires a stable lease identifier.", nameof(request));
        if (request.MaximumCount is < 1 or > WorldEventOutboxLimits.MaximumReadCount)
            throw new ArgumentOutOfRangeException(nameof(request),
                $"At most {WorldEventOutboxLimits.MaximumReadCount} rows may be leased at once.");
        if (request.LeaseSeconds is < WorldEventOutboxLimits.MinimumLeaseSeconds or > WorldEventOutboxLimits.MaximumLeaseSeconds)
            throw new ArgumentOutOfRangeException(nameof(request),
                $"Lease duration must be from {WorldEventOutboxLimits.MinimumLeaseSeconds} to " +
                $"{WorldEventOutboxLimits.MaximumLeaseSeconds} seconds.");
    }

    private static bool IsRetryableTransactionFailure(CaeriusNetSqlException exception)
    {
        return exception.InnerException is SqlException { Number: var errorNumber } &&
               errorNumber is ErrorDeadlockVictim or ErrorWriteConflict or ErrorDependencyFailure or
                   ErrorCommitDependencyAborted;
    }
}
