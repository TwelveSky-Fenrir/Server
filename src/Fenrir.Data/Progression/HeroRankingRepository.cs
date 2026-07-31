using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Data.Progression;

public sealed record HeroRankingRepository(ICaeriusNetDbContext Db, ICaeriusNetCache Cache) : IHeroRankingRepository
{
    private const int CurrentPeriodResultSetCapacity = 64;
    private const int PreviousPeriodResultSetCapacity = 40;
    private static readonly TimeSpan CurrentPeriodTtl = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PreviousPeriodTtl = TimeSpan.FromMinutes(5);

    public async ValueTask<ReadOnlyCollection<HeroRankingRowDto>> GetByPeriodAsync(byte periodKind,
        CancellationToken ct)
    {
        var capacity = periodKind == 0 ? CurrentPeriodResultSetCapacity : PreviousPeriodResultSetCapacity;
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_GetByPeriod", capacity)
            .AddParameter("PeriodKind", periodKind, SqlDbType.TinyInt)
            .AddInMemoryCache(CacheKey(periodKind), periodKind == 0 ? CurrentPeriodTtl : PreviousPeriodTtl)
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
            .AddParameter("Level", (object?)(short?)level ?? DBNull.Value, SqlDbType.SmallInt)
            .AddParameter("RewardClaimed", true, SqlDbType.Bit)
            .Build();

        await Db.ExecuteAsync(sp, ct);
        await Cache.RemoveAsync(CacheKey(periodKind), ct);
    }

    public async ValueTask ClaimRewardAsync(int characterId, byte periodKind, int contributionPointsDelta,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_ClaimReward", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("PeriodKind", periodKind, SqlDbType.TinyInt)
            .AddParameter("ContributionPointsDelta", contributionPointsDelta, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
        await Cache.RemoveAsync(CacheKey(periodKind), ct);
    }

    public async ValueTask<bool> RolloverIfDueAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_Rollover", 1).Build();
        var rolledOver = await Db.ExecuteScalarAsync<bool>(sp, ct);

        if (rolledOver)
        {
            await Cache.RemoveAsync(CacheKey(0), ct);
            await Cache.RemoveAsync(CacheKey(1), ct);
        }

        return rolledOver;
    }

    public async ValueTask<int> AddPointsAsync(int characterId, byte periodKind, int delta, byte? tribeId,
        int? level, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_AddPoints", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("PeriodKind", periodKind, SqlDbType.TinyInt)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .AddParameter("TribeId", (object?)tribeId ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("Level", (object?)(short?)level ?? DBNull.Value, SqlDbType.SmallInt)
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

    private static string CacheKey(byte periodKind)
    {
        return $"game:hero-ranking:{periodKind}";
    }
}
