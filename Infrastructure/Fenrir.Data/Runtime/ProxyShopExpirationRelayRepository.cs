using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

public sealed record ProxyShopExpirationRelayRepository(ICaeriusNetDbContext Db)
    : IProxyShopExpirationRelayRepository
{
    private const int CommandTimeoutSeconds = 5;

    public async ValueTask PublishAsync(ProxyShopExpirationRelayEntry entry, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_ProxyShopExpirationRelay_Publish", 0,
                CommandTimeoutSeconds)
            .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
            .AddParameter("CharacterId", entry.CharacterId, SqlDbType.Int)
            .AddParameter("NewExpirationDate", entry.NewExpirationDate, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<ProxyShopExpirationRelayDto>> PollAsync(byte shardId,
        int retentionSeconds, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_ProxyShopExpirationRelay_Poll", 16,
                CommandTimeoutSeconds)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
            .Build();

        return await Db.QueryAsImmutableArrayAsync<ProxyShopExpirationRelayDto>(sp, ct);
    }
}
