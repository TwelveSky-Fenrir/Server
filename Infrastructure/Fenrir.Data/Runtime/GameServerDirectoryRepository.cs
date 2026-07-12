using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record GameServerDirectoryRepository(ICaeriusNetDbContext Db, ICaeriusNetCache Cache)
    : IGameServerDirectoryRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    // runtime.GameServerDirectory is MEMORY_OPTIMIZED = ON: a shard's own periodic Heartbeat can race the
    // Login side's TcpShardReachabilityProbe-driven MarkUnreachable for the exact same ShardId row (a shard
    // "resurrecting" at the same instant it's being evicted for a stale probe) -- UPDLOCK/ROWLOCK aren't an
    // option against a memory-optimized table, so retry-on-conflict is the correct mechanism here too, same
    // shape as AccountSessionRepository/CharacterShardLocationRepository.
    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask HeartbeatAsync(byte shardId, string host, int port, int ccu, int capacity,
        float tickP99Ms, CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
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

            try
            {
                await Db.ExecuteAsync(parameters, ct);
                return;
            }
            catch (SqlException ex) when (attempt < MaxWriteConflictAttempts && IsWriteConflict(ex.Number))
            {
            }
        }
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
        for (var attempt = 1;; attempt++)
        {
            var parameters =
                new StoredProcedureParametersBuilder("runtime", "usp_GameServer_MarkUnreachable", 0,
                        CommandTimeoutSeconds)
                    .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                    .Build();

            try
            {
                await Db.ExecuteAsync(parameters, ct).ConfigureAwait(false);
                break;
            }
            catch (SqlException ex) when (attempt < MaxWriteConflictAttempts && IsWriteConflict(ex.Number))
            {
            }
        }

        await Cache.RemoveAsync($"shards:directory:{GameServerDirectoryDefaults.StalenessCutoffSeconds}", ct)
            .ConfigureAwait(false);
    }

    private static bool IsWriteConflict(int errorNumber)
    {
        return errorNumber is ErrorWriteConflict or ErrorDependencyFailure or ErrorCommitDependencyAborted;
    }
}
