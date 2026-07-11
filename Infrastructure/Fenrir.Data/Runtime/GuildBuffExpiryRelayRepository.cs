using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

public sealed record GuildBuffExpiryRelayRepository(ICaeriusNetDbContext Db) : IGuildBuffExpiryRelayRepository
{
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
