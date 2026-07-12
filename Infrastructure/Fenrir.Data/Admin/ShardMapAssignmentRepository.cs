using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

public sealed record ShardMapAssignmentRepository(ICaeriusNetDbContext Db) : IShardMapAssignmentRepository
{
    // Neither bound is a hard domain constant (unlike e.g. MAX_USER_AVATAR_NUM) -- world.Zones seeds 117
    // distinct maps as of this writing, so 128/64 are "no fixed bound known, size generously" capacity
    // hints per the caeriusnet-repository-conventions skill, not row caps; an oversized value never
    // truncates or errors, it only avoids a resize as the M1 single-shard-hosts-4-maps seed grows toward a
    // real multi-shard map split.
    private const int AllAssignmentsCapacity = 128;
    private const int HostedMapsPerShardCapacity = 64;

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

        // ReadOnlyCollection<T> already implements IReadOnlyList<T> (this method's own return type) --
        // no .ToArray() double-materialization needed, unlike the sibling GetHostedMapsAsync above whose
        // .Select(...).ToArray() is legitimate because it actually changes element type (DTO -> short).
        return await Db.QueryAsReadOnlyCollectionAsync<ShardMapAssignmentDto>(sp, ct);
    }
}
