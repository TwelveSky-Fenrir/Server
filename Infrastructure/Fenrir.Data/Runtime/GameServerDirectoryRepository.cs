using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Runtime;

/// <summary>
///     Access to runtime.GameServerDirectory (architecture reference §12.4): one warm row per live shard, kept
///     fresh by each shard's own heartbeat so Login can pick a destination without fanning out to every shard.
/// </summary>
public sealed record GameServerDirectoryRepository(ICaeriusNetDbContext Db)
{
    // In-memory OLTP table, sub-millisecond procs -- a short timeout fails fast instead of masking a stuck request.
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

    /// <summary>
    ///     2s in-memory cache (architecture reference §11.4): the directory only needs to be fresh enough for a
    ///     login decision, so this spares a round-trip to runtime.GameServerDirectory on every single login.
    /// </summary>
    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(CancellationToken ct)
    {
        var parameters =
            new StoredProcedureParametersBuilder("runtime", "usp_GameServer_GetDirectory", 16, CommandTimeoutSeconds)
                .AddInMemoryCache("shards:directory", TimeSpan.FromSeconds(2))
                .Build();

        return Db.QueryAsImmutableArrayAsync<ShardDirectoryEntryDto>(parameters, ct);
    }
}
