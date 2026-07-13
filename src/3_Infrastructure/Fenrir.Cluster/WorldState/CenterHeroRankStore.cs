using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Cluster.WorldState;

/// <summary>
///     CaeriusNet-backed persistence for the Center hero-rank current-period accrual. Reuses the existing
///     current-period procedures: <c>usp_HeroRanking_GetByPeriod</c> (PeriodKind 0) for the boot load and
///     <c>usp_HeroRanking_Upsert</c> (RewardClaimed/Description left NULL) for the wholesale write.
/// </summary>
/// <remarks>
///     Deliberately does NOT call <c>usp_HeroRanking_AddPoints</c>: its UPDATE branch freezes the stored Level
///     (the shard-side replication of legacy Bug 1). <see cref="HeroRankAuthority" /> owns the authoritative
///     running total and monotonic level in memory and flushes it wholesale through the idempotent Upsert, which
///     corrects Bug 1. The current-period row carries no reward-claim/description state (those belong to the
///     previous period after rollover), so passing them as NULL on the Upsert is intended, per
///     <see cref="ICenterHeroRankStore" />.
/// </remarks>
public sealed record CenterHeroRankStore(ICaeriusNetDbContext Db) : ICenterHeroRankStore
{
    private const byte CurrentPeriodKind = 0;
    private const int CurrentPeriodResultSetCapacity = 64;

    public async ValueTask<IReadOnlyList<CenterHeroRankAccrual>> LoadCurrentPeriodAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_GetByPeriod",
                CurrentPeriodResultSetCapacity)
            .AddParameter("PeriodKind", CurrentPeriodKind, SqlDbType.TinyInt)
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<HeroRankingRowDto>(sp, ct);

        var accruals = new CenterHeroRankAccrual[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            accruals[i] = new CenterHeroRankAccrual(row.CharacterId, (byte)(row.TribeId ?? 0), row.Points,
                row.Level ?? 0);
        }

        return accruals;
    }

    public ValueTask UpsertCurrentPeriodAsync(int characterId, int points, byte tribeId, int level,
        CancellationToken ct)
    {
        // RewardClaimed and Description are intentionally omitted -- both default to NULL in the procedure,
        // which is the correct current-period state (see the type remarks above).
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_Upsert", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("PeriodKind", CurrentPeriodKind, SqlDbType.TinyInt)
            .AddParameter("Points", points, SqlDbType.Int)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("Level", (short)level, SqlDbType.SmallInt)
            .Build();

        return Db.ExecuteAsync(sp, ct);
    }
}
