using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

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

    public async ValueTask<IReadOnlyList<ShardMapAssignmentDto>> GetAllAssignmentsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_ShardMapAssignment_GetAll").Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<ShardMapAssignmentDto>(sp, ct);
        return rows.ToArray();
    }
}
