using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

public sealed record RvrSiegeEventRelayRepository(ICaeriusNetDbContext Db) : IRvrSiegeEventRelayRepository
{
    private const int CommandTimeoutSeconds = 5;

    public async ValueTask PublishAsync(RvrSiegeEventRelayEntry entry, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_RvrSiegeEventRelay_Publish", 0,
                CommandTimeoutSeconds)
            .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
            .AddParameter("Sort", entry.Sort, SqlDbType.Int)
            .AddParameter("Data", entry.Data, SqlDbType.VarBinary)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<RvrSiegeEventRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_RvrSiegeEventRelay_Poll", 16,
                CommandTimeoutSeconds)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
            .Build();

        return await Db.QueryAsImmutableArrayAsync<RvrSiegeEventRelayDto>(sp, ct);
    }
}
