using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Cluster.Center.WorldState;

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
            accruals[i] = new CenterHeroRankAccrual(row.CharacterId, row.TribeId ?? 0, row.Points,
                row.Level ?? 0);
        }

        return accruals;
    }

    public ValueTask UpsertCurrentPeriodAsync(int characterId, int points, byte tribeId, int level,
        CancellationToken ct)
    {
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
