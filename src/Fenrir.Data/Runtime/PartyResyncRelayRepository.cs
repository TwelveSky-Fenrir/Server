using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using CaeriusNet.Exceptions;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record PartyResyncRelayRepository(ICaeriusNetDbContext Db) : IPartyResyncRelayRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask PublishAsync(PartyResyncRelayEntry entry, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_PartyResyncRelay_Publish", 0,
                CommandTimeoutSeconds)
            .AddParameter("Sort", entry.Sort, SqlDbType.TinyInt)
            .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
            .AddParameter("SourceCharacterId", entry.SourceCharacterId, SqlDbType.Int)
            .AddParameter("RecipientCharacterId", entry.RecipientCharacterId == 0
                ? entry.SourceCharacterId
                : entry.RecipientCharacterId, SqlDbType.Int)
            .AddParameter("PartyName", entry.PartyName, SqlDbType.NVarChar)
            .AddParameter("AvatarName", entry.AvatarName, SqlDbType.NVarChar)
            .AddParameter("CorrelationId", entry.CorrelationId, SqlDbType.UniqueIdentifier)
            .AddParameter("RequestCorrelationId", entry.RequestCorrelationId, SqlDbType.UniqueIdentifier)
            .AddParameter("MemberId1", entry.MemberId1, SqlDbType.Int)
            .AddParameter("MemberName1", entry.MemberName1, SqlDbType.NVarChar)
            .AddParameter("MemberId2", entry.MemberId2, SqlDbType.Int)
            .AddParameter("MemberName2", entry.MemberName2, SqlDbType.NVarChar)
            .AddParameter("MemberId3", entry.MemberId3, SqlDbType.Int)
            .AddParameter("MemberName3", entry.MemberName3, SqlDbType.NVarChar)
            .AddParameter("MemberId4", entry.MemberId4, SqlDbType.Int)
            .AddParameter("MemberName4", entry.MemberName4, SqlDbType.NVarChar)
            .AddParameter("MemberId5", entry.MemberId5, SqlDbType.Int)
            .AddParameter("MemberName5", entry.MemberName5, SqlDbType.NVarChar)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<PartyResyncRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_PartyResyncRelay_Poll", 19,
                    CommandTimeoutSeconds)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
                .Build();

            try
            {
                return await Db.QueryAsImmutableArrayAsync<PartyResyncRelayDto>(sp, ct);
            }
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts &&
                      ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                      IsWriteConflict(sqlErrorNumber))
            {
            }
        }
    }

    public async ValueTask AcknowledgeAsync(byte shardId, long relayId, CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_PartyResyncRelay_Acknowledge", 0,
                    CommandTimeoutSeconds)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .AddParameter("RelayId", relayId, SqlDbType.BigInt)
                .Build();

            try
            {
                await Db.ExecuteAsync(sp, ct);
                return;
            }
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts &&
                      ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                      IsWriteConflict(sqlErrorNumber))
            {
            }
        }
    }

    private static bool IsWriteConflict(int errorNumber)
    {
        return errorNumber is ErrorWriteConflict or ErrorDependencyFailure or ErrorCommitDependencyAborted;
    }
}
