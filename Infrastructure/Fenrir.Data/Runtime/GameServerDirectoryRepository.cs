using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

public sealed record GameServerDirectoryRepository(ICaeriusNetDbContext Db, ICaeriusNetCache Cache)
    : IGameServerDirectoryRepository
{
    private const int CommandTimeoutSeconds = 5;

    public ValueTask HeartbeatAsync(byte shardId, string host, int port, int ccu, int capacity, float tickP99Ms,
        CancellationToken ct)
    {
        var parameters =
            new StoredProcedureParametersBuilder("runtime", "usp_GameServer_Heartbeat", 0, CommandTimeoutSeconds)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .AddParameter("Host", host, SqlDbType.NVarChar)
                .AddParameter("Port", port, SqlDbType.Int)
                .AddParameter("Ccu", ccu, SqlDbType.Int)
                .AddParameter("Capacity", capacity, SqlDbType.Int)
                .AddParameter("TickP99Ms", tickP99Ms, SqlDbType.Real)
                .Build();

        return Db.ExecuteAsync(parameters, ct);
    }

    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(CancellationToken ct)
    {
        return GetDirectoryAsync(GameServerDirectoryDefaults.StalenessCutoffSeconds, ct);
    }

    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(int stalenessCutoffSeconds,
        CancellationToken ct)
    {
        var parameters =
            new StoredProcedureParametersBuilder("runtime", "usp_GameServer_GetDirectory", 16, CommandTimeoutSeconds)
                .AddParameter("StalenessCutoffSeconds", stalenessCutoffSeconds, SqlDbType.Int)
                .AddInMemoryCache($"shards:directory:{stalenessCutoffSeconds}", TimeSpan.FromSeconds(2))
                .Build();

        return Db.QueryAsImmutableArrayAsync<ShardDirectoryEntryDto>(parameters, ct);
    }

    public async ValueTask MarkUnreachableAsync(byte shardId, CancellationToken ct)
    {
        var parameters =
            new StoredProcedureParametersBuilder("runtime", "usp_GameServer_MarkUnreachable", 0,
                    CommandTimeoutSeconds)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .Build();

        await Db.ExecuteAsync(parameters, ct).ConfigureAwait(false);
        await Cache.RemoveAsync($"shards:directory:{GameServerDirectoryDefaults.StalenessCutoffSeconds}", ct)
            .ConfigureAwait(false);
    }
}
