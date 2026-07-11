using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

// Cross-shard fan-out transport for the party-membership resync-on-reconnect flow -- see
// IPartyResyncRelayRepository for the per-method contract. Fan-out sibling of GuildTribeBroadcastRelayRepository
// (SourceShardId <> @ShardId poll filter).
public sealed record PartyResyncRelayRepository(ICaeriusNetDbContext Db) : IPartyResyncRelayRepository
{
    // Small, high-frequency round trips against a memory-optimized table that should never itself be slow --
    // a short timeout fails fast instead of masking a stuck request. Same value as this feature family's
    // siblings (GuildTribeBroadcastRelayRepository, ChatCrossShardRelayRepository).
    private const int CommandTimeoutSeconds = 5;

    public async ValueTask PublishAsync(PartyResyncRelayEntry entry, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_PartyResyncRelay_Publish", 0,
                CommandTimeoutSeconds)
            .AddParameter("Sort", entry.Sort, SqlDbType.TinyInt)
            .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
            .AddParameter("SourceCharacterId", entry.SourceCharacterId, SqlDbType.Int)
            .AddParameter("PartyName", entry.PartyName, SqlDbType.NVarChar)
            .AddParameter("AvatarName", entry.AvatarName, SqlDbType.NVarChar)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<PartyResyncRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_PartyResyncRelay_Poll", 16,
                CommandTimeoutSeconds)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
            .Build();

        return await Db.QueryAsImmutableArrayAsync<PartyResyncRelayDto>(sp, ct);
    }
}
