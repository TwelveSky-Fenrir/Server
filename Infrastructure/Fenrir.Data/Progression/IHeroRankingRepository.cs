using System.Collections.ObjectModel;

namespace Fenrir.Data.Progression;

/// <summary>Abstraction over Fenrir.Data.Progression.HeroRankingRepository for DI/testability.</summary>
public interface IHeroRankingRepository
{
    public ValueTask<ReadOnlyCollection<HeroRankingRowDto>> GetByPeriodAsync(byte periodKind, CancellationToken ct);

    public ValueTask MarkRewardClaimedAsync(int characterId, int points, byte? tribeId, int? level,
        CancellationToken ct);
}
