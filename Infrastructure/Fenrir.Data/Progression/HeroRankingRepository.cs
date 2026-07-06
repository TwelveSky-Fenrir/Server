using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Progression;

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

    /// <summary>
    ///     usp_HeroRanking_Upsert replaces the whole row, not a partial patch -- caller resupplies
    ///     Points/TribeId/Level from the prior read. <paramref name="periodKind" /> must be 1 (Previous) for
    ///     CZ_HEROREWARD_SEND: the legacy's hAccept/hCharIdx claim-state arrays are populated only from the
    ///     rankType==0 DB branch, which feeds RANK_INFO.mPrevious (S08_MyDB.cpp:249-284), not mCurrent.
    /// </summary>
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

    /// <summary>
    ///     usp_HeroRanking_Rollover gates the actual Current-&gt;Previous flip on a 7-day sentinel it owns
    ///     itself (game.HeroRankingRolloverState) -- most calls are a no-op, which is expected since every
    ///     shard's <c>HeroRankingRolloverHost</c> polls this independently rather than coordinating.
    /// </summary>
    public async ValueTask<bool> RolloverIfDueAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_HeroRanking_Rollover", 1).Build();
        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

    /// <summary>
    ///     usp_HeroRanking_AddPoints does the accumulate atomically in SQL (<c>Points = Points + @Delta</c>),
    ///     unlike <see cref="MarkRewardClaimedAsync" />'s whole-row overwrite -- see that proc's own header
    ///     comment and <see cref="Fenrir.Data.Abstractions.Progression.IHeroRankingRepository.AddPointsAsync" />'s
    ///     remarks for why this distinction matters for a per-kill grant.
    /// </summary>
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
}
