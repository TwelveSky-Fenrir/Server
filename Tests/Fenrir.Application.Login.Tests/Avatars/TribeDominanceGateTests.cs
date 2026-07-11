using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Application.Login.Tests.Avatars;

public class TribeDominanceGateTests
{
    [Fact]
    public void BlocksCreation_NoStandingsAtAll_NeverBlocks()
    {
        Assert.False(TribeDominanceGate.BlocksCreation(0, []));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    public void BlocksCreation_LeaderBelowTheFloor_NeverBlocksRegardlessOfImbalance(int leaderPoints)
    {
        var standings = new[]
        {
            new TribeSummaryDto(0, null, leaderPoints),
            new TribeSummaryDto(1, null, 0),
            new TribeSummaryDto(2, null, 0),
            new TribeSummaryDto(3, null, 0)
        };

        Assert.False(TribeDominanceGate.BlocksCreation(0, standings));
    }

    [Fact]
    public void BlocksCreation_RequestedTribeIsTheSoleLeaderAtTheFloor_Blocks()
    {
        var standings = new[]
        {
            new TribeSummaryDto(0, null, 40),
            new TribeSummaryDto(1, null, 100),
            new TribeSummaryDto(2, null, 30),
            new TribeSummaryDto(3, null, 20)
        };

        Assert.True(TribeDominanceGate.BlocksCreation(1, standings));
    }

    [Fact]
    public void BlocksCreation_RequestedTribeIsNotTheLeader_DoesNotBlockEvenAboveTheFloor()
    {
        var standings = new[]
        {
            new TribeSummaryDto(0, null, 10),
            new TribeSummaryDto(1, null, 150),
            new TribeSummaryDto(2, null, 0),
            new TribeSummaryDto(3, null, 0)
        };

        Assert.False(TribeDominanceGate.BlocksCreation(0, standings));
    }

    [Fact]
    public void BlocksCreation_TiedForHighest_OnlyTheLowestNumberedTiedTribeIsTheLeader()
    {
        var standings = new[]
        {
            new TribeSummaryDto(0, null, 0),
            new TribeSummaryDto(1, null, 100),
            new TribeSummaryDto(2, null, 100),
            new TribeSummaryDto(3, null, 0)
        };

        Assert.True(TribeDominanceGate.BlocksCreation(1, standings));
        Assert.False(TribeDominanceGate.BlocksCreation(2, standings));
    }

    [Fact]
    public void BlocksCreation_FourthTribeSlotHoldsTheLead_BecomesANoOpForEveryPlayableTribe()
    {
        var standings = new[]
        {
            new TribeSummaryDto(0, null, 5),
            new TribeSummaryDto(1, null, 5),
            new TribeSummaryDto(2, null, 5),
            new TribeSummaryDto(3, null, 999)
        };

        Assert.False(TribeDominanceGate.BlocksCreation(0, standings));
        Assert.False(TribeDominanceGate.BlocksCreation(1, standings));
        Assert.False(TribeDominanceGate.BlocksCreation(2, standings));
    }

    [Fact]
    public void BlocksCreation_TribeAbsentFromStandings_TreatedAsZeroPoints()
    {
        var standings = new[] { new TribeSummaryDto(1, null, 150) };

        Assert.True(TribeDominanceGate.BlocksCreation(1, standings));
        Assert.False(TribeDominanceGate.BlocksCreation(0, standings));
    }

    [Fact]
    public void BlocksCreation_NullPointsTotal_TreatedAsZero()
    {
        var standings = new[]
        {
            new TribeSummaryDto(0, null, null),
            new TribeSummaryDto(1, null, null),
            new TribeSummaryDto(2, null, null),
            new TribeSummaryDto(3, null, null)
        };

        Assert.False(TribeDominanceGate.BlocksCreation(0, standings));
    }
}
