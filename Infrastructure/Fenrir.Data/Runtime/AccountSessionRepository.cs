using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

// Cross-process duplicate-login kick/refusal authority (runtime.AccountSessions) -- see
// IAccountSessionRepository for the per-method contract.
public sealed record AccountSessionRepository(ICaeriusNetDbContext Db) : IAccountSessionRepository
{
    // In-memory OLTP table, sub-millisecond procs -- a short timeout fails fast instead of masking a stuck request.
    private const int CommandTimeoutSeconds = 5;

    // Native-compiled write-write conflict errors on a memory-optimized table under SNAPSHOT isolation: the
    // loser of a genuine concurrent claim race gets one of these at commit, never silently-wrong data.
    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    // No backoff: these resolve instantly, it's a single-row in-memory conflict, not contention worth waiting out.
    private const int MaxClaimAttempts = 3;

    public async ValueTask<AccountSessionClaimDto> ClaimOrSignalKickAsync(int accountId, Guid newSessionToken,
        CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            // AttemptNumber > 1 is only ever reachable below because our own previous attempt's INSERT for
            // this exact accountId already lost a write-write conflict against a concurrent claim -- it lets
            // the procedure tell "row just committed by that concurrent winner" apart from "genuinely stale
            // row abandoned by an unrelated earlier session" instead of deleting the winner's row outright.
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_AccountSession_ClaimOrSignalKick", 1,
                    CommandTimeoutSeconds)
                .AddParameter("AccountId", accountId, SqlDbType.Int)
                .AddParameter("NewSessionToken", newSessionToken, SqlDbType.UniqueIdentifier)
                .AddParameter("AttemptNumber", (byte)attempt, SqlDbType.TinyInt)
                .Build();

            try
            {
                return await Db.FirstQueryAsync<AccountSessionClaimDto>(sp, ct) ??
                       throw new InvalidOperationException(
                           "usp_AccountSession_ClaimOrSignalKick always returns exactly one row.");
            }
            catch (SqlException ex) when (attempt < MaxClaimAttempts && IsClaimWriteConflict(ex.Number))
            {
                // Loser of a genuine concurrent claim race -- retry immediately, the winner already committed.
            }
        }
    }

    /// <summary>
    ///     Covers both a fresh Login-&gt;Game world-entry AND a Game(ShardA)-&gt;Game(ShardB) cross-shard zone
    ///     transfer -- same procedure, same predicate shape, widened since
    ///     Database/Migrations/025_account_session_transition_game_to_game.sql to accept a prior ServerKind of
    ///     either Login or Game. See IAccountSessionRepository.TransitionToGameAsync's own remarks.
    /// </summary>
    public async ValueTask<bool> TransitionToGameAsync(int accountId, Guid expectedSessionToken, byte shardId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_AccountSession_TransitionToGame", 1,
                CommandTimeoutSeconds)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("ExpectedSessionToken", expectedSessionToken, SqlDbType.UniqueIdentifier)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

    public async ValueTask MarkTearingDownAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_AccountSession_MarkTearingDown", 0,
                CommandTimeoutSeconds)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("ServerKind", (byte)serverKind, SqlDbType.TinyInt)
            .AddParameter("ShardId", (object?)shardId ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("SessionToken", sessionToken, SqlDbType.UniqueIdentifier)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask ClearIfOwnerAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_AccountSession_ClearIfOwner", 0,
                CommandTimeoutSeconds)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("ServerKind", (byte)serverKind, SqlDbType.TinyInt)
            .AddParameter("ShardId", (object?)shardId ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("SessionToken", sessionToken, SqlDbType.UniqueIdentifier)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     Never called with an empty accountIds collection by contract; guarded here anyway since SQL Server rejects an
    ///     empty TVP outright.
    /// </summary>
    public async ValueTask<ImmutableArray<KickedAccountDto>> RefreshAndGetKickedAsync(
        AccountSessionServerKind serverKind, byte? shardId, IReadOnlyCollection<int> accountIds, CancellationToken ct)
    {
        if (accountIds.Count == 0)
            return ImmutableArray<KickedAccountDto>.Empty;

        var rows = accountIds.Select(id => new AccountIdTvp(id)).ToArray();

        var sp = new StoredProcedureParametersBuilder("runtime", "usp_AccountSession_RefreshAndGetKicked",
                accountIds.Count, CommandTimeoutSeconds)
            .AddParameter("ServerKind", (byte)serverKind, SqlDbType.TinyInt)
            .AddParameter("ShardId", (object?)shardId ?? DBNull.Value, SqlDbType.TinyInt)
            .AddTvpParameter("AccountIds", rows)
            .Build();

        return await Db.QueryAsImmutableArrayAsync<KickedAccountDto>(sp, ct);
    }

    public async ValueTask<ImmutableArray<ReapedAccountSessionDto>> ReapStaleAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_AccountSession_ReapStale", 16,
                CommandTimeoutSeconds)
            .Build();

        return await Db.QueryAsImmutableArrayAsync<ReapedAccountSessionDto>(sp, ct);
    }

    /// <summary>Never cached: ServerQuotaRefreshHost controls the refresh cadence (~1s), not this repository.</summary>
    public async ValueTask<int> GetActiveSessionCountAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_AccountSession_GetActiveCount", 1,
                CommandTimeoutSeconds)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    private static bool IsClaimWriteConflict(int errorNumber)
    {
        return errorNumber is ErrorWriteConflict or ErrorDependencyFailure or ErrorCommitDependencyAborted;
    }
}
