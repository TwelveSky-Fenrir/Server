using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record PartyResyncRelayRepository(ICaeriusNetDbContext Db) : IPartyResyncRelayRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    // usp_PartyResyncRelay_Poll's own reap-delete sweeps runtime.PartyResyncRelay by a flat CreatedAtUtc-cutoff
    // predicate shared by every shard's own independent poll cycle -- see ChatCrossShardRelayRepository's own
    // remarks for why two shards' concurrent reaps can race the same expiring row and why that's worth
    // retrying rather than dropping a whole poll cycle over.
    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask PublishAsync(PartyResyncRelayEntry entry, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_PartyResyncRelay_Publish", 0,
                CommandTimeoutSeconds)
            .AddParameter("Sort", entry.Sort, SqlDbType.TinyInt)
            .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
            .AddParameter("SourceCharacterId", entry.SourceCharacterId, SqlDbType.Int)
            .AddParameter("PartyName", entry.PartyName, SqlDbType.NVarChar)
            .AddParameter("AvatarName", entry.AvatarName, SqlDbType.NVarChar)
            .AddParameter("CorrelationId", entry.CorrelationId, SqlDbType.UniqueIdentifier)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<PartyResyncRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_PartyResyncRelay_Poll", 16,
                    CommandTimeoutSeconds)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
                .Build();

            try
            {
                return await Db.QueryAsImmutableArrayAsync<PartyResyncRelayDto>(sp, ct);
            }
            catch (SqlException ex) when (attempt < MaxWriteConflictAttempts && IsWriteConflict(ex.Number))
            {
            }
        }
    }

    private static bool IsWriteConflict(int errorNumber)
    {
        return errorNumber is ErrorWriteConflict or ErrorDependencyFailure or ErrorCommitDependencyAborted;
    }
}
