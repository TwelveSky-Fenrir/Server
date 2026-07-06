using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class TribePointLevelRecomputeServiceTests
{
    private sealed class FakeRosterGateway : ITribePointRosterGateway
    {
        public IReadOnlyList<TribeRosterCharacterSnapshot>? Roster { get; set; } = [];
        public bool Throw { get; set; }
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<TribeRosterCharacterSnapshot>?> GetRosterAsync(CancellationToken ct)
        {
            CallCount++;
            if (Throw)
                throw new InvalidOperationException("simulated roster-read failure");

            return Task.FromResult(Roster);
        }
    }

    private static (WorldStateService WorldState, FakeWorldStateRepository Repository) CreateInitializedWorldState()
    {
        var repository = new FakeWorldStateRepository();
        var worldState = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return (worldState, repository);
    }

    [Fact]
    public async Task RosterAvailable_OverwritesAllFourTribeTotals()
    {
        var (worldState, _) = CreateInitializedWorldState();
        var gateway = new FakeRosterGateway
        {
            Roster =
            [
                new TribeRosterCharacterSnapshot(0, 200, 0, 0), // +88
                new TribeRosterCharacterSnapshot(3, 145, 0, 0) // +33, plus tribe-3's own +800
            ]
        };
        var service = new TribePointLevelRecomputeService(gateway, worldState,
            NullLogger<TribePointLevelRecomputeService>.Instance);

        await service.RecomputeAsync(CancellationToken.None);

        Assert.Equal(1000 + 88, worldState.GetTribe(0).Points);
        Assert.Equal(1000, worldState.GetTribe(1).Points);
        Assert.Equal(1000, worldState.GetTribe(2).Points);
        Assert.Equal(1000 + 33 + 800, worldState.GetTribe(3).Points);
    }

    [Fact]
    public async Task RosterUnavailable_Null_AbortsWithoutWritingAnyTotals()
    {
        var (worldState, _) = CreateInitializedWorldState();
        worldState.SetTribePoints(0, 4242); // pre-existing standing that must survive an aborted run
        var gateway = new FakeRosterGateway { Roster = null };
        var service = new TribePointLevelRecomputeService(gateway, worldState,
            NullLogger<TribePointLevelRecomputeService>.Instance);

        await service.RecomputeAsync(CancellationToken.None);

        Assert.Equal(4242, worldState.GetTribe(0).Points);
        // Never recomputed at all in this aborted run, so tribe 1 keeps whatever it was zero-initialized to
        // at boot (per TribePointLevelRecomputeService's own remarks: "the tribe-point totals are
        // zero-initialized at boot, the value that persists if every recompute attempt fails") -- 0, not
        // TribePointLevelRecompute.Baseline (1000), which only ever appears once a recompute has actually run.
        Assert.Equal(0, worldState.GetTribe(1).Points);
    }

    [Fact]
    public async Task RosterReadThrows_AbortsWithoutWritingAnyTotals_AndNeverPropagates()
    {
        var (worldState, _) = CreateInitializedWorldState();
        worldState.SetTribePoints(2, 777);
        var gateway = new FakeRosterGateway { Throw = true };
        var service = new TribePointLevelRecomputeService(gateway, worldState,
            NullLogger<TribePointLevelRecomputeService>.Instance);

        await service.RecomputeAsync(CancellationToken.None);

        Assert.Equal(777, worldState.GetTribe(2).Points);
    }

    [Fact]
    public async Task LoggingOnlyGateway_AlwaysReturnsNull_NeverThrows()
    {
        var gateway = new LoggingOnlyTribePointRosterGateway(
            NullLogger<LoggingOnlyTribePointRosterGateway>.Instance);

        var result = await gateway.GetRosterAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task EachRecompute_IsAFullOverwrite_NotAnIncrementalDelta()
    {
        var (worldState, _) = CreateInitializedWorldState();
        var gateway = new FakeRosterGateway
        {
            Roster = [new TribeRosterCharacterSnapshot(0, 200, 0, 0)] // +88
        };
        var service = new TribePointLevelRecomputeService(gateway, worldState,
            NullLogger<TribePointLevelRecomputeService>.Instance);

        await service.RecomputeAsync(CancellationToken.None);
        Assert.Equal(1000 + 88, worldState.GetTribe(0).Points);

        // A second recompute with a shrunk roster must land on the freshly recomputed value, not add on top of
        // the previous run's result.
        gateway.Roster = [];
        await service.RecomputeAsync(CancellationToken.None);
        Assert.Equal(1000, worldState.GetTribe(0).Points);
    }
}
