using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Application.Login.Tests.Avatars;

// op17's dominant-tribe creation gate: absolute >=100 floor on the leading tribe's own total, no margin/lead
// computation. Réf. C++ : Server/ts25login/S04_MyWork02.cpp:724-730 ; Server/Header/function.h:3124-3144.
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
        // Tribe 1 is dominant (150 points); requesting tribe 0 is unaffected -- this is an absolute floor on
        // the leader's own total, never a margin/gap comparison between tribes.
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
        // Tribes 1 and 2 are exactly tied at 100; the scan only replaces the leader on a strictly-greater
        // total, so tribe 1 (lower-numbered) remains "the" leader and tribe 2 is never blocked despite being
        // equally dominant.
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
        // Tribe 3 (excluded as a creation target upstream of this gate) holds the highest total. The
        // requested tribe can only ever be 0-2 by the time this runs, so the equality check can never be
        // true and the gate never blocks -- regardless of how lopsided 0-2 are relative to each other.
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
        // Only tribe 1 has a row at all (e.g. a partially-seeded WorldStateTribes); tribes 0/2/3 default to 0
        // and never contend for the lead.
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
