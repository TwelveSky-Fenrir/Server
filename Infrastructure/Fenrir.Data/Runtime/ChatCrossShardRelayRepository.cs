using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

// Cross-shard delivery for the whisper (op39 / SECRET_CHAT) point-to-point chat flow -- see
// IChatCrossShardRelayRepository for the per-method contract. Point-to-point sibling of
// SocialCrossShardRelayRepository (TargetShardId = @ShardId poll filter), not fan-out.
public sealed record ChatCrossShardRelayRepository(ICaeriusNetDbContext Db) : IChatCrossShardRelayRepository
{
    // Small, high-frequency round trips against a memory-optimized table that should never itself be slow --
    // a short timeout fails fast instead of masking a stuck request. Same value as this feature's sibling,
    // SocialCrossShardRelayRepository.
    private const int CommandTimeoutSeconds = 5;

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
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<ChatCrossShardWhisperDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_ChatCrossShardRelay_Poll", 16,
                CommandTimeoutSeconds)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
            .Build();

        return await Db.QueryAsImmutableArrayAsync<ChatCrossShardWhisperDto>(sp, ct);
    }
}
