using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Progression;

/// <summary>
///     game.HeroRankings access (Server Logic chapter, V9 Progression) -- CZ_HERORANK_INFO_SEND (118, read,
///     throttled 2.5s per period) and CZ_HEROREWARD_SEND (119, claim). Singleton, same posture as every
///     other repository in this project.
/// </summary>
public sealed record HeroRankingRepository(ICaeriusNetDbContext Db)
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
    ///     Marks a character's CURRENT-period row as reward-claimed (usp_HeroRanking_Upsert) -- the caller
    ///     re-supplies the row's own Points/TribeId/Level (already known from the immediately-preceding
    ///     <see cref="GetByPeriodAsync" /> read) since the upsert proc replaces the whole row, not a
    ///     partial patch.
    /// </summary>
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
