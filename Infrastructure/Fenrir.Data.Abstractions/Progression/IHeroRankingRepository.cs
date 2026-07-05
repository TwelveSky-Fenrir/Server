using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Progression;

public interface IHeroRankingRepository
{
    public ValueTask<ReadOnlyCollection<HeroRankingRowDto>> GetByPeriodAsync(byte periodKind, CancellationToken ct);

    public ValueTask MarkRewardClaimedAsync(int characterId, byte periodKind, int points, byte? tribeId, int? level,
        CancellationToken ct);

    /// <summary>
    ///     Calls game.usp_HeroRanking_Rollover, which flips Current-&gt;Previous once 7 real days have
    ///     elapsed since the last flip. Idempotent and safe to call redundantly/concurrently from every
    ///     shard on every tick -- returns whether THIS call was the one that performed the flip.
    /// </summary>
    public ValueTask<bool> RolloverIfDueAsync(CancellationToken ct);
}
