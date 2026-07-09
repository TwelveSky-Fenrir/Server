using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

// Cross-shard fan-out for the six character-to-character negotiation flows (Party invite, Friend ask,
// Mentor ask, Duel ask, Trade invite, Guild invite) -- see ISocialCrossShardRelayRepository for the
// per-method contract.
public sealed record SocialCrossShardRelayRepository(ICaeriusNetDbContext Db) : ISocialCrossShardRelayRepository
{
    // Small, high-frequency round trips against a memory-optimized table that should never itself be slow --
    // a short timeout fails fast instead of masking a stuck request. Same value as this feature's fan-out
    // sibling, GuildTribeBroadcastRelayRepository.
    private const int CommandTimeoutSeconds = 5;

    public async ValueTask PublishAsync(SocialCrossShardRelayEntry entry, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_SocialCrossShardRelay_Publish", 0,
                CommandTimeoutSeconds)
            .AddParameter("Kind", (byte)entry.Kind, SqlDbType.TinyInt)
            .AddParameter("MessageType", (byte)entry.MessageType, SqlDbType.TinyInt)
            .AddParameter("Accepted", (object?)entry.Accepted ?? DBNull.Value, SqlDbType.Bit)
            .AddParameter("ReasonCode", (object?)entry.ReasonCode ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
            .AddParameter("SourceCharacterId", entry.SourceCharacterId, SqlDbType.Int)
            .AddParameter("SourceAvatarName", entry.SourceAvatarName, SqlDbType.NVarChar)
            .AddParameter("TargetShardId", entry.TargetShardId, SqlDbType.TinyInt)
            .AddParameter("TargetCharacterId", entry.TargetCharacterId, SqlDbType.Int)
            .AddParameter("AskRelayId", (object?)entry.AskRelayId ?? DBNull.Value, SqlDbType.BigInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<SocialCrossShardRelayDto>> PollAsync(byte shardId,
        int retentionSeconds, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_SocialCrossShardRelay_Poll", 16,
                CommandTimeoutSeconds)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
            .Build();

        return await Db.QueryAsImmutableArrayAsync<SocialCrossShardRelayDto>(sp, ct);
    }
}
