using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Progression;

public interface IHeroRankingRepository
{
    public ValueTask<ReadOnlyCollection<HeroRankingRowDto>> GetByPeriodAsync(byte periodKind, CancellationToken ct);

    public ValueTask MarkRewardClaimedAsync(int characterId, byte periodKind, int points, byte? tribeId, int? level,
        CancellationToken ct);

        public ValueTask<int> AddPointsAsync(int characterId, byte periodKind, int delta, byte? tribeId, int? level,
        CancellationToken ct);

        public ValueTask<bool> RolloverIfDueAsync(CancellationToken ct);

        public ValueTask<int?> GetPointsAsync(int characterId, byte periodKind, CancellationToken ct);
}
