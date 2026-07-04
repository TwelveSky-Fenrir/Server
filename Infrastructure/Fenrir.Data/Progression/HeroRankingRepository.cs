using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Progression;

// CZ_HERORANK_INFO_SEND (118, read, throttled 2.5s/period) and CZ_HEROREWARD_SEND (119, claim).
public sealed record HeroRankingRepository(ICaeriusNetDbContext Db) : IHeroRankingRepository
{
    /// <summary>PeriodKind 0 (Current) or 1 (Previous), leaderboard-ordered (Points DESC).</summary>
    public async ValueTask<ReadOnlyCollection<HeroRankingRowDto>> GetByPeriodAsync(byte periodKind,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_GetByPeriod", 40)
            .AddParameter("PeriodKind", periodKind, SqlDbType.TinyInt)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<HeroRankingRowDto>(sp, ct);
    }

    /// <summary>usp_HeroRanking_Upsert replaces the whole row, not a partial patch -- caller resupplies Points/TribeId/Level from the prior read.</summary>
    public async ValueTask MarkRewardClaimedAsync(int characterId, int points, byte? tribeId, int? level,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_Upsert", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("PeriodKind", (byte)0, SqlDbType.TinyInt)
            .AddParameter("Points", points, SqlDbType.Int)
            .AddParameter("TribeId", (object?)tribeId ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("Level", (object?)level ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("RewardClaimed", true, SqlDbType.Bit)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
