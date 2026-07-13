namespace Fenrir.Cluster.WorldState;

/// <summary>One character's current-period hero-rank accrual snapshot loaded at boot.</summary>
public readonly record struct CenterHeroRankAccrual(int CharacterId, byte TribeId, int Points, int Level);

/// <summary>
///     The persistence seam for the Center hero-rank current-period accrual. DECLARED here (minimal) so
///     <see cref="HeroRankAuthority" /> compiles and stays repository-oriented; the implementation is owned by
///     Main / the data unit and needs NO new stored procedure -- it wraps the existing
///     <c>usp_HeroRanking_GetByPeriod</c> (PeriodKind 0) for the load and <c>usp_HeroRanking_Upsert</c> (with
///     <c>RewardClaimed = NULL</c>, <c>Description = NULL</c>) for the wholesale current-period write.
/// </summary>
/// <remarks>
///     Deliberately not reusing <c>IHeroRankingRepository.AddPointsAsync</c>: that proc freezes Level on the
///     UPDATE branch (a legacy-faithful, shard-side replication of Bug 1). The Center authority CORRECTS Bug 1
///     by accumulating points and tracking level monotonically in memory, then flushing the authoritative
///     total+level wholesale -- which the wholesale Upsert makes naturally idempotent.
/// </remarks>
public interface ICenterHeroRankStore
{
    /// <summary>Loads the current-period (PeriodKind 0) accrual for every ranked character.</summary>
    ValueTask<IReadOnlyList<CenterHeroRankAccrual>> LoadCurrentPeriodAsync(CancellationToken ct);

    /// <summary>Wholesale-writes one character's current-period accrual (points, tribe, monotonic level).</summary>
    ValueTask UpsertCurrentPeriodAsync(int characterId, int points, byte tribeId, int level, CancellationToken ct);
}
