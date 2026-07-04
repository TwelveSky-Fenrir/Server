using System.Collections.ObjectModel;

namespace Fenrir.Data.Progression;

public interface IHeroRankingRepository
{
    public ValueTask<ReadOnlyCollection<HeroRankingRowDto>> GetByPeriodAsync(byte periodKind, CancellationToken ct);

    public ValueTask MarkRewardClaimedAsync(int characterId, int points, byte? tribeId, int? level,
        CancellationToken ct);
}
