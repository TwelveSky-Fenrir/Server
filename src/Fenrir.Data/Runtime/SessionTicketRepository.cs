using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using CaeriusNet.Exceptions;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record SessionTicketRepository(ICaeriusNetDbContext Db) : ISessionTicketRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask CreateAsync(int accountId, int characterId, byte shardId, int ttlSeconds,
        Guid sessionToken, short accountGrade, short targetMapId, CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var parameters =
                new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Create", 0, CommandTimeoutSeconds)
                    .AddParameter("AccountId", accountId, SqlDbType.Int)
                    .AddParameter("CharacterId", characterId, SqlDbType.Int)
                    .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                    .AddParameter("TtlSeconds", ttlSeconds, SqlDbType.Int)
                    .AddParameter("SessionToken", sessionToken, SqlDbType.UniqueIdentifier)
                    .AddParameter("AccountGrade", accountGrade, SqlDbType.SmallInt)
                    .AddParameter("TargetMapId", targetMapId, SqlDbType.SmallInt)
                    .Build();

            try
            {
                await Db.ExecuteAsync(parameters, ct);
                return;
            }
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts && ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                      IsWriteConflict(sqlErrorNumber))
            {
            }
        }
    }

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
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts && ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                      IsWriteConflict(sqlErrorNumber))
            {
            }
        }
    }

    public async ValueTask RevokeAsync(int accountId, CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var parameters =
                new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Revoke", 0, CommandTimeoutSeconds)
                    .AddParameter("AccountId", accountId, SqlDbType.Int)
                    .Build();

            try
            {
                await Db.ExecuteAsync(parameters, ct);
                return;
            }
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts && ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                      IsWriteConflict(sqlErrorNumber))
            {
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

    private static bool IsWriteConflict(int errorNumber)
    {
        return errorNumber is ErrorWriteConflict or ErrorDependencyFailure or ErrorCommitDependencyAborted;
    }
}
