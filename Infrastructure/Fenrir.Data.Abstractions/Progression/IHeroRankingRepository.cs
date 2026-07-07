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

    /// <summary>
    ///     Reads the character's currently stored Points for <paramref name="periodKind" /> -- the
    ///     world-entry hydration read this project's behavior contract maps to legacy's login-time
    ///     <c>MyDB::GetHeroPoint</c> (Server/ts25login/S08_MyDB.cpp:1178-1188). Null means no row exists yet
    ///     for this (CharacterId, PeriodKind) pair (the character never earned a hero-rank point during that
    ///     period) -- legacy's own exactly-one-row success gate collapses that case, and an outright query
    ///     failure, to the identical zero outcome; callers are expected to default a null result to zero
    ///     exactly like legacy's own unconditional pre-query initialization does, not treat it as an error.
    /// </summary>
    public ValueTask<int?> GetPointsAsync(int characterId, byte periodKind, CancellationToken ct);
}
