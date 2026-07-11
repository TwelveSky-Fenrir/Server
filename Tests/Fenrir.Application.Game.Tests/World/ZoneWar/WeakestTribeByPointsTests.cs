using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.World.WorldState;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class WeakestTribeByPointsTests
{
    [Fact]
    public void PicksStrictlyLowestPointTotal()
    {
        Assert.Equal(2, WeakestTribeByPoints.Resolve([500, 300, 100, 400]));
    }

    [Fact]
    public void TieResolvesToLowestTribeIndex()
    {
        Assert.Equal(1, WeakestTribeByPoints.Resolve([50, 10, 50, 10]));
    }

    [Fact]
    public void AllEqual_ReturnsTribeZero()
    {
        Assert.Equal(0, WeakestTribeByPoints.Resolve([0, 0, 0, 0]));
    }

    [Fact]
    public void NegativeTotalsAreLowerThanZero()
    {
        Assert.Equal(3, WeakestTribeByPoints.Resolve([0, 0, 0, -1]));
    }

    [Fact]
    public void WrongCount_Throws()
    {
        Assert.Throws<ArgumentException>(() => WeakestTribeByPoints.Resolve([1, 2, 3]));
    }

    [Fact]
    public void Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => WeakestTribeByPoints.Resolve((IReadOnlyList<int>)null!));
    }

    [Fact]
    public async Task WorldStateOverload_ReadsCachedPointsInTribeIdOrder()
    {
        var worldState = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        await worldState.InitializeAsync(CancellationToken.None);

        worldState.SetTribePoints(0, 900);
        worldState.SetTribePoints(1, 250);
        worldState.SetTribePoints(2, 700);
        worldState.SetTribePoints(3, 700);

        Assert.Equal(1, WeakestTribeByPoints.Resolve(worldState));
    }

    [Fact]
    public async Task WorldStateOverload_FreshBoot_AllZero_ReturnsTribeZero()
    {
        var worldState = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        await worldState.InitializeAsync(CancellationToken.None);

        Assert.Equal(0, WeakestTribeByPoints.Resolve(worldState));
    }
}
