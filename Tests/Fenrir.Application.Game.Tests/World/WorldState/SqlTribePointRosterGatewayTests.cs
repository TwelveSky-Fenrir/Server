using System.Collections.ObjectModel;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class SqlTribePointRosterGatewayTests
{
    [Fact]
    public async Task MapsEveryField_TribeLevelLevel2Rebirth()
    {
        var repository = new FakeTribeRosterRepository
        {
            Rows =
            [
                new TribeRosterCharacterDto(2, 175, 6, 4),
                new TribeRosterCharacterDto(0, 145, 1, 0)
            ]
        };
        var gateway = new SqlTribePointRosterGateway(repository);

        var roster = await gateway.GetRosterAsync(CancellationToken.None);

        Assert.NotNull(roster);
        Assert.Equal(2, roster!.Count);
        Assert.Equal(new TribeRosterCharacterSnapshot(2, 175, 6, 4), roster[0]);
        Assert.Equal(new TribeRosterCharacterSnapshot(0, 145, 1, 0), roster[1]);
    }

    [Fact]
    public async Task EmptyRoster_ReturnsEmptyList_NotNull()
    {
        var gateway = new SqlTribePointRosterGateway(new FakeTribeRosterRepository { Rows = [] });

        var roster = await gateway.GetRosterAsync(CancellationToken.None);

        Assert.NotNull(roster);
        Assert.Empty(roster!);
    }

    [Fact]
    public async Task MappedRoster_FeedsComputeTotals_ToTheExpectedPerTribeTotals()
    {
        var repository = new FakeTribeRosterRepository
        {
            Rows =
            [
                new TribeRosterCharacterDto(0, 200, 0, 0),
                new TribeRosterCharacterDto(3, 145, 2, 1)
            ]
        };
        var gateway = new SqlTribePointRosterGateway(repository);

        var roster = await gateway.GetRosterAsync(CancellationToken.None);
        var totals = TribePointLevelRecompute.ComputeTotals(roster!);

        Assert.Equal(1000 + 88, totals[0]);
        Assert.Equal(1000, totals[1]);
        Assert.Equal(1000, totals[2]);
        Assert.Equal(1000 + 42 + 800, totals[3]);
    }

    [Fact]
    public async Task RepositoryThrows_Propagates_SoTheRecomputeServicesCatchCanAbortTheRun()
    {
        var gateway = new SqlTribePointRosterGateway(new FakeTribeRosterRepository
        {
            Throw = new InvalidOperationException("simulated roster read failure")
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.GetRosterAsync(CancellationToken.None));
    }

    private sealed class FakeTribeRosterRepository : ITribeRosterRepository
    {
        public List<TribeRosterCharacterDto> Rows { get; init; } = [];
        public Exception? Throw { get; init; }

        public ValueTask<ReadOnlyCollection<TribeRosterCharacterDto>> GetForTribePointAsync(CancellationToken ct)
        {
            if (Throw is { } ex)
                throw ex;

            return ValueTask.FromResult(new ReadOnlyCollection<TribeRosterCharacterDto>(Rows));
        }
    }
}
