using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Cluster.WorldState;

public sealed record CenterTribeScoreSource(ICaeriusNetDbContext Db) : ICenterTribeScoreSource
{
    private const int TribeResultSetCapacity = 4;

    public async ValueTask<IReadOnlyList<CenterTribeStatAggregate>> ComputeAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_WorldState_RecomputeTribeScores",
            TribeResultSetCapacity).Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<CenterTribeStatAggregateRowDto>(sp, ct);

        var aggregates = new CenterTribeStatAggregate[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            aggregates[i] = new CenterTribeStatAggregate(rows[i].TribeId, rows[i].StatSum);

        return aggregates;
    }
}
