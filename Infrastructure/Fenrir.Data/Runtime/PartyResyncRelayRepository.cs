using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

public sealed record PartyResyncRelayRepository(ICaeriusNetDbContext Db) : IPartyResyncRelayRepository
{
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
