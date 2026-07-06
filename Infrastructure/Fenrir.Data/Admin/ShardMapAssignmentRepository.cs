using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

// DB-backed replacement for the Game:Maps config list; GameServer resolves hosted maps here at boot, before ZoneRegistry.Initialize builds one Zone actor per map id.
public sealed record ShardMapAssignmentRepository(ICaeriusNetDbContext Db) : IShardMapAssignmentRepository
{
    public async ValueTask<IReadOnlyList<short>> GetHostedMapsAsync(byte shardId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_ShardMapAssignment_GetForShard", 1)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<ShardMapAssignmentRowDto>(sp, ct);
        return rows.Select(row => row.MapId).ToArray();
    }

    // Liveness-independent: no @ShardId parameter, returns every row in the table. See ShardPartitionGuard
    // for why a boot-time overlap check needs this in addition to the per-shard lookup above.
    public async ValueTask<IReadOnlyList<ShardMapAssignmentDto>> GetAllAssignmentsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_ShardMapAssignment_GetAll", 16).Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<ShardMapAssignmentDto>(sp, ct);
        return rows.ToArray();
    }
}
