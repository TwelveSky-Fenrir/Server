using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class SingletonRvrSchedulerValidatorTests
{
    [Fact]
    public void AllDesignatedMapsAtZero_ReportsNothing()
    {
        SingletonRvrSchedulerValidator.DesignatedMapClaim[] designatedMaps =
        [
            new("VoteTribe", 0),
            new("HolyStoneBattle", 0)
        ];

        var unclaimed = SingletonRvrSchedulerValidator.FindUnclaimed(designatedMaps, []);

        Assert.Empty(unclaimed);
    }

    [Fact]
    public void DesignatedMapIsClaimedByALiveShard_ReportsNothing()
    {
        SingletonRvrSchedulerValidator.DesignatedMapClaim[] designatedMaps = [new("VoteTribe", 37)];

        var unclaimed = SingletonRvrSchedulerValidator.FindUnclaimed(designatedMaps, [37]);

        Assert.Empty(unclaimed);
    }

    [Fact]
    public void DesignatedMapIsNotClaimedByAnyLiveShard_ReportsIt()
    {
        SingletonRvrSchedulerValidator.DesignatedMapClaim[] designatedMaps = [new("VoteTribe", 37)];

        var unclaimed = SingletonRvrSchedulerValidator.FindUnclaimed(designatedMaps, [1, 2]);

        var entry = Assert.Single(unclaimed);
        Assert.Equal("VoteTribe", entry.SchedulerName);
        Assert.Equal((short)37, entry.MapId);
    }

    [Fact]
    public void MultipleDesignatedMaps_OnlyTheUnclaimedOnesAreReported()
    {
        SingletonRvrSchedulerValidator.DesignatedMapClaim[] designatedMaps =
        [
            new("VoteTribe", 37),
            new("HolyStoneBattle", 37),
            new("HolyStoneWar", 38)
        ];

        var unclaimed = SingletonRvrSchedulerValidator.FindUnclaimed(designatedMaps, [37]);

        var entry = Assert.Single(unclaimed);
        Assert.Equal("HolyStoneWar", entry.SchedulerName);
        Assert.Equal((short)38, entry.MapId);
    }

    [Fact]
    public void DuplicateSchedulerNameAndMapIdEntries_AreEachIndependentlyEvaluated()
    {
        SingletonRvrSchedulerValidator.DesignatedMapClaim[] designatedMaps =
        [
            new("VoteTribe", 37),
            new("VoteTribe", 37)
        ];

        var unclaimed = SingletonRvrSchedulerValidator.FindUnclaimed(designatedMaps, []);

        Assert.Equal(2, unclaimed.Count);
    }
}
