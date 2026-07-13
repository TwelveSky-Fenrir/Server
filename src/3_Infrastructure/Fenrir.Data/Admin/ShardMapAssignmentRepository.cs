using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

public sealed record ShardMapAssignmentRepository(ICaeriusNetDbContext Db) : IShardMapAssignmentRepository
{
    // Un seul shard peut désormais héberger l'intégralité du catalogue de zones (seed 027 assigne toute
    // world.Zones à shard 1) : dimensionner les hints de pré-allocation au-delà des ~117 maps.
    private const int AllAssignmentsCapacity = 256;
    private const int HostedMapsPerShardCapacity = 256;

    public async ValueTask<IReadOnlyList<short>> GetHostedMapsAsync(byte shardId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_ShardMapAssignment_GetForShard",
                HostedMapsPerShardCapacity)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<ShardMapAssignmentRowDto>(sp, ct);
        return rows.Select(row => row.MapId).ToArray();
    }

    public async ValueTask<IReadOnlyList<ShardMapAssignmentDto>> GetAllAssignmentsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_ShardMapAssignment_GetAll",
            AllAssignmentsCapacity).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ShardMapAssignmentDto>(sp, ct);
    }
}
