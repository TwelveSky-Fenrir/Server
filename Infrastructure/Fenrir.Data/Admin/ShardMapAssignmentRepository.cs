using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;

namespace Fenrir.Data.Admin;

/// <summary>
///     admin.ShardMapAssignments access -- the DB-backed replacement for the Game:Maps config list
///     (GameServerOptions' own remarks): GameServer resolves its hosted maps here once at boot, the
///     same way WorldDataLoader resolves world.* reference data, before ZoneRegistry.Initialize builds
///     one Zone actor per returned map id.
/// </summary>
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
}
