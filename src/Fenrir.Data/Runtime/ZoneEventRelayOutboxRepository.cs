using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Exceptions;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record ZoneEventRelayOutboxRepository(ICaeriusNetDbContext Db) : IZoneEventRelayOutboxRepository
{
    private const int CommandTimeoutSeconds = 5;
    private const int ErrorDeadlockVictim = 1205;
    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;
    private const int MaximumRetryAttempts = 3;

    public async ValueTask<ZoneEventRelayOutboxEnqueueResultDto> EnqueueAsync(ZoneEventRelayOutboxEntry entry,
        CancellationToken ct)
    {
        ValidateEntry(entry);

        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_ZoneEventRelayOutbox_Enqueue", 1,
                    CommandTimeoutSeconds)
                .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
                .AddParameter("Sort", entry.Sort, SqlDbType.Int)
                .AddParameter("Data", entry.Data, SqlDbType.VarBinary, ZoneEventRelayOutboxLimits.PayloadSize)
                .AddParameter("OperationId", entry.OperationId, SqlDbType.UniqueIdentifier)
                .AddParameter("CorrelationId", entry.CorrelationId, SqlDbType.UniqueIdentifier)
                .Build();

            try
            {
                return await Db.FirstQueryAsync<ZoneEventRelayOutboxEnqueueResultDto>(sp, ct).ConfigureAwait(false) ??
                       throw new InvalidOperationException(
                           "usp_ZoneEventRelayOutbox_Enqueue always returns an enqueue result.");
            }
            catch (CaeriusNetSqlException ex) when (attempt < MaximumRetryAttempts && IsRetryable(ex))
            {
            }
        }
    }

    public async ValueTask<ImmutableArray<ZoneEventRelayOutboxDeliveryDto>> ClaimAsync(
        ZoneEventRelayOutboxClaimRequest request, CancellationToken ct)
    {
        ValidateClaim(request);

        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_ZoneEventRelayOutbox_Claim",
                    request.MaximumCount, CommandTimeoutSeconds)
                .AddParameter("SourceShardId", request.SourceShardId, SqlDbType.TinyInt)
                .AddParameter("LeaseId", request.LeaseId, SqlDbType.UniqueIdentifier)
                .AddParameter("MaximumCount", request.MaximumCount, SqlDbType.Int)
                .AddParameter("LeaseSeconds", request.LeaseSeconds, SqlDbType.Int)
                .Build();

            try
            {
                return await Db.QueryAsImmutableArrayAsync<ZoneEventRelayOutboxDeliveryDto>(sp, ct)
                    .ConfigureAwait(false);
            }
            catch (CaeriusNetSqlException ex) when (attempt < MaximumRetryAttempts && IsRetryable(ex))
            {
            }
        }
    }

    public async ValueTask<bool> AcknowledgeAsync(ZoneEventRelayOutboxAcknowledgement acknowledgement,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        if (acknowledgement.OutboxId <= 0)
            throw new ArgumentOutOfRangeException(nameof(acknowledgement));
        if (acknowledgement.SourceShardId == 0 || acknowledgement.LeaseId == Guid.Empty)
            throw new ArgumentException("A relay acknowledgement requires its shard and lease identity.",
                nameof(acknowledgement));

        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_ZoneEventRelayOutbox_Acknowledge", 1,
                    CommandTimeoutSeconds)
                .AddParameter("OutboxId", acknowledgement.OutboxId, SqlDbType.BigInt)
                .AddParameter("SourceShardId", acknowledgement.SourceShardId, SqlDbType.TinyInt)
                .AddParameter("LeaseId", acknowledgement.LeaseId, SqlDbType.UniqueIdentifier)
                .Build();

            try
            {
                return (await Db.FirstQueryAsync<ZoneEventRelayOutboxAcknowledgeResultDto>(sp, ct)
                            .ConfigureAwait(false) ??
                        throw new InvalidOperationException(
                            "usp_ZoneEventRelayOutbox_Acknowledge always returns an acknowledgement result."))
                    .Acknowledged;
            }
            catch (CaeriusNetSqlException ex) when (attempt < MaximumRetryAttempts && IsRetryable(ex))
            {
            }
        }
    }

    private static void ValidateEntry(ZoneEventRelayOutboxEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.SourceShardId == 0)
            throw new ArgumentOutOfRangeException(nameof(entry), "Zone event relay messages require a shard.");
        if (entry.Data is not { Length: ZoneEventRelayOutboxLimits.PayloadSize })
            throw new ArgumentOutOfRangeException(nameof(entry),
                $"Zone event relay data must be exactly {ZoneEventRelayOutboxLimits.PayloadSize} bytes.");
        if (entry.OperationId == Guid.Empty || entry.CorrelationId == Guid.Empty)
            throw new ArgumentException("Zone event relay messages require operation and correlation identifiers.",
                nameof(entry));
    }

    private static void ValidateClaim(ZoneEventRelayOutboxClaimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceShardId == 0 || request.LeaseId == Guid.Empty)
            throw new ArgumentException("A relay claim requires its shard and lease identity.", nameof(request));
        if (request.MaximumCount is < 1 or > ZoneEventRelayOutboxLimits.MaximumClaimCount)
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.LeaseSeconds is < ZoneEventRelayOutboxLimits.MinimumLeaseSeconds or
            > ZoneEventRelayOutboxLimits.MaximumLeaseSeconds)
            throw new ArgumentOutOfRangeException(nameof(request));
    }

    private static bool IsRetryable(CaeriusNetSqlException exception)
    {
        return exception.InnerException is SqlException { Number: var errorNumber } &&
               errorNumber is ErrorDeadlockVictim or ErrorWriteConflict or ErrorDependencyFailure or
                   ErrorCommitDependencyAborted;
    }
}
