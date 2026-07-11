using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Data.Progression;

public sealed record HeroRankingRepository(ICaeriusNetDbContext Db) : IHeroRankingRepository
{

        public async ValueTask<ReadOnlyCollection<HeroRankingRowDto>> GetByPeriodAsync(byte periodKind,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_GetByPeriod", 40)
            .AddParameter("PeriodKind", periodKind, SqlDbType.TinyInt)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<HeroRankingRowDto>(sp, ct);
    }

        public async ValueTask MarkRewardClaimedAsync(int characterId, byte periodKind, int points, byte? tribeId,
        int? level, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_Upsert", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("PeriodKind", periodKind, SqlDbType.TinyInt)
            .AddParameter("Points", points, SqlDbType.Int)
            .AddParameter("TribeId", (object?)tribeId ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("Level", (object?)level ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("RewardClaimed", true, SqlDbType.Bit)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask<bool> RolloverIfDueAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_Rollover", 1).Build();
        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

        public async ValueTask<int> AddPointsAsync(int characterId, byte periodKind, int delta, byte? tribeId,
        int? level, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_AddPoints", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("PeriodKind", periodKind, SqlDbType.TinyInt)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .AddParameter("TribeId", (object?)tribeId ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("Level", (object?)level ?? DBNull.Value, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

        public async ValueTask<int?> GetPointsAsync(int characterId, byte periodKind, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_GetPoints", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("PeriodKind", periodKind, SqlDbType.TinyInt)
            .Build();

        var row = await Db.FirstQueryAsync<HeroRankingPointsDto>(sp, ct);
        return row?.Points;
    }
}
