using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

internal sealed class FakeTowerRepository : ITowerRepository
{
    public List<TowerStateRowDto> Rows { get; set; } =
        [.. Enumerable.Range(0, 12).Select(i => new TowerStateRowDto((byte)i, 0, 0, null, null))];

    public int EnsureInitializedCallCount { get; private set; }
    public List<(byte TowerIndex, byte Level, byte TowerType, byte? ControllingTribeId)> SetProgressCalls { get; } = [];
    public bool ThrowOnSetProgress { get; set; }

    public ValueTask EnsureInitializedAsync(CancellationToken ct)
    {
        EnsureInitializedCallCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask<ReadOnlyCollection<TowerStateRowDto>> GetAllAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(new ReadOnlyCollection<TowerStateRowDto>(Rows));
    }

    public ValueTask SetProgressAsync(byte towerIndex, byte level, byte towerType, byte? controllingTribeId,
        CancellationToken ct)
    {
        if (ThrowOnSetProgress)
            throw new InvalidOperationException("Simulated SQL failure");

        SetProgressCalls.Add((towerIndex, level, towerType, controllingTribeId));
        return ValueTask.CompletedTask;
    }
}
