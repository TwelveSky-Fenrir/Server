using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

// Tickets are keyed on AccountId, not a generated TicketId -- the unmodified legacy client can only prove its own account identity.
public sealed record SessionTicketRepository(ICaeriusNetDbContext Db) : ISessionTicketRepository
{
    // In-memory OLTP table, sub-millisecond procs -- a short timeout fails fast instead of masking a stuck request.
    private const int CommandTimeoutSeconds = 5;

    // Same native-compiled write-write conflict class as AccountSessionRepository -- runtime.SessionTickets is
    // the identical shape (memory-optimized, hash index on AccountId, natively compiled, SNAPSHOT isolation):
    // the loser of a genuine concurrent consume race gets one of these at commit, never silently-wrong data.
    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    // No backoff: same rationale as AccountSessionRepository -- this resolves instantly, a single-row in-memory
    // conflict, not contention worth waiting out.
    private const int MaxConsumeAttempts = 3;

    public ValueTask CreateAsync(int accountId, int characterId, byte shardId, int ttlSeconds, Guid sessionToken,
        short accountGrade, CancellationToken ct)
    {
        var parameters =
            new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Create", 0, CommandTimeoutSeconds)
                .AddParameter("AccountId", accountId, SqlDbType.Int)
                .AddParameter("CharacterId", characterId, SqlDbType.Int)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .AddParameter("TtlSeconds", ttlSeconds, SqlDbType.Int)
                .AddParameter("SessionToken", sessionToken, SqlDbType.UniqueIdentifier)
                .AddParameter("AccountGrade", accountGrade, SqlDbType.SmallInt)
                .Build();

        return Db.ExecuteAsync(parameters, ct);
    }

    // usp_SessionTicket_Consume's own "not idempotent, never retryable" warning targets a caller blindly
    // re-invoking this method after already observing an outcome (that would risk re-consuming/duplicating a
    // still-valid ticket). This retry is a different, narrower case: it only fires when the natively-compiled
    // atomic block itself never committed any outcome at all -- a write-write conflict at commit, caught below
    // by SqlException.Number -- so the whole BEGIN ATOMIC block rolled back and nothing was consumed to
    // duplicate. A retried attempt then either observes the winner's already-committed DELETE (the ordinary
    // empty-result "no ticket" outcome ZoneHandshakeService already treats as a normal rejection) or, on a
    // still-contended attempt, conflicts again and retries up to the bound below.
    public async ValueTask<ConsumedTicketDto?> ConsumeAsync(int accountId, CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var parameters =
                new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Consume", 1, CommandTimeoutSeconds)
                    .AddParameter("AccountId", accountId, SqlDbType.Int)
                    .Build();

            try
            {
                return await Db.FirstQueryAsync<ConsumedTicketDto>(parameters, ct);
            }
            catch (SqlException ex) when (attempt < MaxConsumeAttempts && IsConsumeWriteConflict(ex.Number))
            {
                // Loser of a genuine concurrent consume race -- retry immediately, the winner's DELETE already
                // committed.
            }
        }
    }

    public ValueTask PurgeExpiredAsync(CancellationToken ct)
    {
        var parameters =
            new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Purge", 0, CommandTimeoutSeconds)
                .Build();

        return Db.ExecuteAsync(parameters, ct);
    }

    private static bool IsConsumeWriteConflict(int errorNumber)
    {
        return errorNumber is ErrorWriteConflict or ErrorDependencyFailure or ErrorCommitDependencyAborted;
    }
}
