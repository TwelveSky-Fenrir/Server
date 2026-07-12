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
    // PeriodKind=0 ("current") is genuinely hot-write (usp_HeroRanking_AddPoints fires per PvP kill), so it
    // gets the same short TTL already accepted for FirewallRule/GmAllowlist/MacRestriction/GameServerDirectory
    // -- deduping concurrent pollers within HeroRankingService's own 2.5s per-character throttle window costs
    // nothing extra per write only because AddPointsAsync deliberately never evicts this key (see below).
    // PeriodKind=1 ("previous") only changes at usp_HeroRanking_Rollover cadence or when a reward is claimed,
    // both of which explicitly evict their own key below, so a much longer TTL is safe here and matches
    // GameSettingsRepository's precedent for data an admin/event-driven action changes, not a tick.
    private static readonly TimeSpan CurrentPeriodTtl = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PreviousPeriodTtl = TimeSpan.FromMinutes(5);

    // Sized on the query's own real cardinality, not on HeroRankBuilder's 4-tribe/10-slot display grid --
    // usp_HeroRanking_GetByPeriod.sql has no TOP/LIMIT on either branch, so the two periods don't share one
    // bound. PeriodKind=1 ("previous") IS provably capped at 40: usp_HeroRanking_Rollover.sql keeps only
    // `Rn <= 10` per `PARTITION BY TribeId`, and TribeId can only be one of 4 values
    // (game.Tribes' CK_Tribes_TribeId CHECK (TribeId BETWEEN 0 AND 3)) -- 40 is a real bound here, not a
    // display-grid coincidence. PeriodKind=0 ("current") has no such cap: usp_HeroRanking_AddPoints accumulates
    // a row per character earning any hero point since the last rollover, unpruned until the next one, so its
    // row count scales with the server's active PvP population -- given the conventional wide/no-fixed-bound
    // capacity hint instead.
    private const int CurrentPeriodResultSetCapacity = 64;
    private const int PreviousPeriodResultSetCapacity = 40;

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

        // Evicting immediately (rather than waiting out PreviousPeriodTtl) is not just an optimization here:
        // HeroRewardClaimService reads GetByPeriodAsync(1, ...) to resolve claimability before calling this
        // method. Without this eviction, a second claim attempt for the same character served from a stale
        // cached "not yet claimed" row would re-enter usp_HeroRanking_ClaimReward's guarded UPDATE and hit its
        // THROW 50357 -- which HeroRewardClaimHandler does not catch -- instead of the graceful AlreadyClaimed
        // outcome a fresh read produces.
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

    private static string CacheKey(byte periodKind)
    {
        return $"game:hero-ranking:{periodKind}";
    }

    // Deliberately does not evict CacheKey(periodKind): this fires per PvP kill, and invalidating on every
    // call would defeat the whole point of caching the hot PeriodKind=0 read -- the CurrentPeriodTtl window
    // is the only staleness bound here, matching GameServerDirectoryRepository.HeartbeatAsync's own
    // no-eviction precedent for its comparably hot write path.
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
}
