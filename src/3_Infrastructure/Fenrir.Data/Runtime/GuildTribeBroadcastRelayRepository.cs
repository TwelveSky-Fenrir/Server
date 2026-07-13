using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record GuildTribeBroadcastRelayRepository(ICaeriusNetDbContext Db)
    : IGuildTribeBroadcastRelayRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask PublishAsync(GuildTribeBroadcastRelayEntry entry, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_GuildTribeBroadcastRelay_Publish", 0,
                CommandTimeoutSeconds)
            .AddParameter("Kind", (byte)entry.Kind, SqlDbType.TinyInt)
            .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
            .AddParameter("GuildId", (object?)entry.GuildId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("Tribe", (object?)entry.Tribe ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("RoleField", entry.RoleField, SqlDbType.TinyInt)
            .AddParameter("AvatarName", entry.AvatarName, SqlDbType.NVarChar)
            .AddParameter("Content", entry.Content, SqlDbType.NVarChar)
            .AddParameter("HasItemLink", entry.HasItemLink, SqlDbType.Bit)
            .AddParameter("ItemLinkIndex", (object?)entry.ItemLinkIndex ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("ItemLinkActivity", (object?)entry.ItemLinkActivity ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("ItemLinkValue", (object?)entry.ItemLinkValue ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("ItemLinkSocket0", (object?)entry.ItemLinkSocket0 ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("ItemLinkSocket1", (object?)entry.ItemLinkSocket1 ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("ItemLinkSocket2", (object?)entry.ItemLinkSocket2 ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("CorrelationId", entry.CorrelationId, SqlDbType.UniqueIdentifier)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<GuildTribeBroadcastRelayDto>> PollAsync(byte shardId,
        int retentionSeconds, CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_GuildTribeBroadcastRelay_Poll", 16,
                    CommandTimeoutSeconds)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
                .Build();

            try
            {
                return await Db.QueryAsImmutableArrayAsync<GuildTribeBroadcastRelayDto>(sp, ct);
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
