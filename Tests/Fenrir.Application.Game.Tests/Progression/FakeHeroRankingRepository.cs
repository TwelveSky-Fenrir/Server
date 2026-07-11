using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

internal sealed class FakeHeroRankingRepository : IHeroRankingRepository
{
    public Dictionary<(int CharacterId, byte PeriodKind), int> Points { get; } = new();
    public List<(int CharacterId, byte PeriodKind, int Delta, byte? TribeId, int? Level)> AddPointsCalls { get; } = [];
    public bool ThrowOnAddPoints { get; set; }

    public ValueTask<ReadOnlyCollection<HeroRankingRowDto>> GetByPeriodAsync(byte periodKind, CancellationToken ct)
    {
        return ValueTask.FromResult(new ReadOnlyCollection<HeroRankingRowDto>([]));
    }

    public ValueTask MarkRewardClaimedAsync(int characterId, byte periodKind, int points, byte? tribeId, int? level,
        CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> RolloverIfDueAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(false);
    }

    public ValueTask<int> AddPointsAsync(int characterId, byte periodKind, int delta, byte? tribeId, int? level,
        CancellationToken ct)
    {
        if (ThrowOnAddPoints)
            throw new InvalidOperationException("Simulated flush failure.");

        AddPointsCalls.Add((characterId, periodKind, delta, tribeId, level));

        var key = (characterId, periodKind);
        var newTotal = Points.GetValueOrDefault(key) + delta;
        Points[key] = newTotal;
        return ValueTask.FromResult(newTotal);
    }

    public ValueTask<int?> GetPointsAsync(int characterId, byte periodKind, CancellationToken ct)
    {
        var key = (characterId, periodKind);
        return ValueTask.FromResult<int?>(Points.TryGetValue(key, out var points) ? points : null);
    }
}
