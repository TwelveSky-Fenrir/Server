using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

// One warm row per live shard, kept fresh by each shard's own heartbeat -- Login picks a destination without fanning out to every shard.
public sealed record GameServerDirectoryRepository(ICaeriusNetDbContext Db, ICaeriusNetCache Cache)
    : IGameServerDirectoryRepository
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
    ///     2s in-memory cache -- the directory only needs to be fresh enough for a login decision. Uses
    ///     <see cref="GameServerDirectoryDefaults.StalenessCutoffSeconds" /> as the staleness cutoff; see
    ///     <see cref="GetDirectoryAsync(int, CancellationToken)" /> for a caller-supplied cutoff.
    /// </summary>
    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(CancellationToken ct)
    {
        return GetDirectoryAsync(GameServerDirectoryDefaults.StalenessCutoffSeconds, ct);
    }

    /// <summary>2s in-memory cache, keyed per cutoff so distinct callers never read each other's stale entry.</summary>
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

    /// <summary>
    ///     Deletes the row via <c>usp_GameServer_MarkUnreachable</c>, then evicts the default-cutoff
    ///     <see cref="GetDirectoryAsync(CancellationToken)" /> read cache -- the only overload any current
    ///     caller (ZoneTransferService, LoginConnectionHost, ShardPartitionGuard, ...) actually uses -- so the
    ///     very next default-cutoff read observes the eviction immediately instead of waiting out its own 2s
    ///     TTL layered on top of the row's own 15s freshness window.
    /// </summary>
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
