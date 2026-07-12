using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record ChatCrossShardRelayRepository(ICaeriusNetDbContext Db) : IChatCrossShardRelayRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    // usp_ChatCrossShardRelay_Poll's own reap-delete sweeps runtime.ChatCrossShardRelay by a flat
    // CreatedAtUtc <= retention-cutoff predicate shared by EVERY shard's own independent poll cycle -- two
    // shards whose poll cycles happen to overlap in wall-clock time while both see the same expired row as a
    // delete candidate provably race on that row (a write-write conflict, not a bug in either shard's own
    // inbound data). Retry rather than let one shard's whole poll cycle -- and every legitimate message it
    // would have delivered -- fail just because of that incidental, unrelated janitorial collision.
    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask PublishAsync(ChatCrossShardWhisperEntry entry, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_ChatCrossShardRelay_Publish", 0,
                CommandTimeoutSeconds)
            .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
            .AddParameter("SourceCharacterId", entry.SourceCharacterId, SqlDbType.Int)
            .AddParameter("SourceAvatarName", entry.SourceAvatarName, SqlDbType.NVarChar)
            .AddParameter("TargetShardId", entry.TargetShardId, SqlDbType.TinyInt)
            .AddParameter("TargetCharacterId", entry.TargetCharacterId, SqlDbType.Int)
            .AddParameter("TargetAvatarName", entry.TargetAvatarName, SqlDbType.NVarChar)
            .AddParameter("Content", entry.Content, SqlDbType.NVarChar)
            .AddParameter("SenderAuthType", entry.SenderAuthType, SqlDbType.TinyInt)
            .AddParameter("CorrelationId", entry.CorrelationId, SqlDbType.UniqueIdentifier)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<ChatCrossShardWhisperDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_ChatCrossShardRelay_Poll", 16,
                    CommandTimeoutSeconds)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
                .Build();

            try
            {
                return await Db.QueryAsImmutableArrayAsync<ChatCrossShardWhisperDto>(sp, ct);
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
