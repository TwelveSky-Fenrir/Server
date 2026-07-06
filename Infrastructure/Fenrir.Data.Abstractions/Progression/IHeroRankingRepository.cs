using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Progression;

public interface IHeroRankingRepository
{
    public ValueTask<ReadOnlyCollection<HeroRankingRowDto>> GetByPeriodAsync(byte periodKind, CancellationToken ct);

    public ValueTask MarkRewardClaimedAsync(int characterId, byte periodKind, int points, byte? tribeId, int? level,
        CancellationToken ct);

    /// <summary>
    ///     Atomically adds <paramref name="delta" /> (may be negative) to the character's stored Points for
    ///     <paramref name="periodKind" />, seeding a fresh row if none exists yet, and returns the new total.
    ///     Unlike <see cref="MarkRewardClaimedAsync" /> (a full-row overwrite via
    ///     <c>usp_HeroRanking_Upsert</c>, correct only for a claim-time write), this is the safe entry point
    ///     for a per-kill point GRANT: two concurrent grants to the same character never clobber each other
    ///     because the increment happens inside one atomic <c>usp_HeroRanking_AddPoints</c> UPDATE, not a
    ///     read-modify-write round trip through application code.
    /// </summary>
    public ValueTask<int> AddPointsAsync(int characterId, byte periodKind, int delta, byte? tribeId, int? level,
        CancellationToken ct);

    /// <summary>
    ///     Calls game.usp_HeroRanking_Rollover, which flips Current-&gt;Previous once 7 real days have
    ///     elapsed since the last flip. Idempotent and safe to call redundantly/concurrently from every
    ///     shard on every tick -- returns whether THIS call was the one that performed the flip.
    /// </summary>
    public ValueTask<bool> RolloverIfDueAsync(CancellationToken ct);
}
