using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

// Cross-shard fan-out for the guild-buff-reserve-exhaustion immediate strip-effect push -- see
// IGuildBuffExpiryRelayRepository for the per-method contract.
public sealed record GuildBuffExpiryRelayRepository(ICaeriusNetDbContext Db) : IGuildBuffExpiryRelayRepository
{
    // Small, rare round trips against a memory-optimized table that should never itself be slow -- a short
    // timeout fails fast instead of masking a stuck request. Same value as GuildTribeBroadcastRelayRepository.
    private const int CommandTimeoutSeconds = 5;

    public async ValueTask PublishAsync(GuildBuffExpiryRelayEntry entry, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_GuildBuffExpiryRelay_Publish", 0,
                CommandTimeoutSeconds)
            .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
            .AddParameter("GuildId", entry.GuildId, SqlDbType.Int)
            .AddParameter("NewBuffTime", entry.NewBuffTime, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<GuildBuffExpiryRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_GuildBuffExpiryRelay_Poll", 3,
                CommandTimeoutSeconds)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
            .Build();

        return await Db.QueryAsImmutableArrayAsync<GuildBuffExpiryRelayDto>(sp, ct);
    }
}
