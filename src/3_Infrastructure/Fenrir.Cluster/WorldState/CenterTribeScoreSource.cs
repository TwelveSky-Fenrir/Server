using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Cluster.WorldState;

/// <summary>
///     CaeriusNet-backed character-stats source for the CenterServer 6-beat tribe-score recompute (Bug 3 fix).
///     Wraps the new <c>game.usp_WorldState_RecomputeTribeScores</c>, which does the per-tribe GROUP BY / SUM in
///     SQL over <c>game.Characters</c> (level &gt;= 145). The 1000 baseline and tribe 3's flat +800 are applied
///     Fenrir-side by <see cref="TribeScoreRecompute" />, not here.
/// </summary>
public sealed record CenterTribeScoreSource(ICaeriusNetDbContext Db) : ICenterTribeScoreSource
{
    // At most one row per tribe (game.Characters.Tribe is CHECK-constrained to 0..3).
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
