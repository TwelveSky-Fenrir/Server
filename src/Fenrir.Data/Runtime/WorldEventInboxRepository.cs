using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Exceptions;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record WorldEventInboxRepository(ICaeriusNetDbContext Db) : IWorldEventInboxRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorDeadlockVictim = 1205;
    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;
    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask<WorldEventInboxReceiptResultDto> ReceiptAsync(WorldEventInboxReceiptRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateDelivery(request.OutboxId, request.DeliveryLeaseId, nameof(request));

        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_WorldInbox_Apply", 1,
                    CommandTimeoutSeconds)
                .AddParameter("OutboxId", request.OutboxId, SqlDbType.BigInt)
                .AddParameter("DestinationShardId", request.DestinationShardId, SqlDbType.TinyInt)
                .AddParameter("DeliveryLeaseId", request.DeliveryLeaseId, SqlDbType.UniqueIdentifier)
                .Build();

            try
            {
                return await Db.FirstQueryAsync<WorldEventInboxReceiptResultDto>(sp, ct) ??
                       throw new InvalidOperationException(
                           "usp_WorldInbox_Apply always returns an inbox receipt result.");
            }
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts && IsRetryableTransactionFailure(ex))
            {
            }
        }
    }

    public async ValueTask<bool> AcknowledgeAsync(WorldEventInboxAcknowledgement acknowledgement,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        ValidateDelivery(acknowledgement.OutboxId, acknowledgement.DeliveryLeaseId, nameof(acknowledgement));

        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_WorldInbox_Acknowledge", 1,
                    CommandTimeoutSeconds)
                .AddParameter("OutboxId", acknowledgement.OutboxId, SqlDbType.BigInt)
                .AddParameter("DestinationShardId", acknowledgement.DestinationShardId, SqlDbType.TinyInt)
                .AddParameter("DeliveryLeaseId", acknowledgement.DeliveryLeaseId, SqlDbType.UniqueIdentifier)
                .Build();

            try
            {
                return (await Db.FirstQueryAsync<WorldEventInboxAcknowledgeResultDto>(sp, ct) ??
                        throw new InvalidOperationException(
                            "usp_WorldInbox_Acknowledge always returns an acknowledgement result."))
                    .Acknowledged;
            }
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts && IsRetryableTransactionFailure(ex))
            {
            }
        }
    }

    private static void ValidateDelivery(long outboxId, Guid deliveryLeaseId, string parameterName)
    {
        if (outboxId <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "The outbox identifier must be positive.");
        if (deliveryLeaseId == Guid.Empty)
            throw new ArgumentException("A world inbox operation requires a delivery lease identifier.", parameterName);
    }

    private static bool IsRetryableTransactionFailure(CaeriusNetSqlException exception)
    {
        return exception.InnerException is SqlException { Number: var errorNumber } &&
               errorNumber is ErrorDeadlockVictim or ErrorWriteConflict or ErrorDependencyFailure or
                   ErrorCommitDependencyAborted;
    }
}
