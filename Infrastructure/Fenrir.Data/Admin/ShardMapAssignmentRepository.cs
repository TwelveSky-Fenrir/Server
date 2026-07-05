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
}
