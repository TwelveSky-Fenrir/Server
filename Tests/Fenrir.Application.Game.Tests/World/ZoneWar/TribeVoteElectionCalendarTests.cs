using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeVoteElectionCalendarTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(15, 12)]
    [InlineData(31, 23)]
    public void RealCalendarDay_FromClosed_AlwaysOpensRegistration(int dayOfMonth, int hourOfDay)
    {
        var transition = TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Closed, dayOfMonth, hourOfDay, false);

        Assert.Equal(TribeVoteCalendarTransition.OpenRegistration, transition);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(15, 12)]
    [InlineData(31, 23)]
    public void RealCalendarDay_FromVotingClosedOrResultsAnnounced_StillOpensRegistration(int dayOfMonth, int hourOfDay)
    {
        Assert.Equal(TribeVoteCalendarTransition.OpenRegistration,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.VotingClosed, dayOfMonth, hourOfDay, false));
        Assert.Equal(TribeVoteCalendarTransition.OpenRegistration,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.ResultsAnnounced, dayOfMonth, hourOfDay, false));
        Assert.Equal(TribeVoteCalendarTransition.OpenRegistration,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Voting, dayOfMonth, hourOfDay, false));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(15, 12)]
    [InlineData(31, 23)]
    public void RealCalendarDay_AlreadyRegistrationOpen_IsIdempotent_NoTransition(int dayOfMonth, int hourOfDay)
    {
        var transition = TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Candidacy, dayOfMonth, hourOfDay, false);

        Assert.Equal(TribeVoteCalendarTransition.None, transition);
    }

    [Fact]
    public void TestMode_IsACompleteNoOp_RegardlessOfDayOrPhase()
    {
        Assert.Equal(TribeVoteCalendarTransition.None,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Closed, 1, 0, true));
        Assert.Equal(TribeVoteCalendarTransition.None,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Voting, 15, 12, true));
    }

    [Fact]
    public void HypotheticalNonCalendarDay_BelowOne_StructurallyReachesTheOtherwiseBranches()
    {
        Assert.Equal(TribeVoteCalendarTransition.OpenVoting,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Closed, 0, 0, false));
        Assert.Equal(TribeVoteCalendarTransition.OpenVoting,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Closed, -1, 0, false));
    }
}
