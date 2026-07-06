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
        var transition = TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Closed, dayOfMonth, hourOfDay, testMode: false);

        Assert.Equal(TribeVoteCalendarTransition.OpenRegistration, transition);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(15, 12)]
    [InlineData(31, 23)]
    public void RealCalendarDay_FromVotingClosedOrResultsAnnounced_StillOpensRegistration(int dayOfMonth, int hourOfDay)
    {
        // Every valid DateTime.Day is >= 1, so the day >= 1 branch is always taken regardless of which phase
        // the cycle happens to be in -- only "already Candidacy" is guarded against.
        Assert.Equal(TribeVoteCalendarTransition.OpenRegistration,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.VotingClosed, dayOfMonth, hourOfDay, testMode: false));
        Assert.Equal(TribeVoteCalendarTransition.OpenRegistration,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.ResultsAnnounced, dayOfMonth, hourOfDay, testMode: false));
        Assert.Equal(TribeVoteCalendarTransition.OpenRegistration,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Voting, dayOfMonth, hourOfDay, testMode: false));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(15, 12)]
    [InlineData(31, 23)]
    public void RealCalendarDay_AlreadyRegistrationOpen_IsIdempotent_NoTransition(int dayOfMonth, int hourOfDay)
    {
        var transition = TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Candidacy, dayOfMonth, hourOfDay, testMode: false);

        Assert.Equal(TribeVoteCalendarTransition.None, transition);
    }

    [Fact]
    public void TestMode_IsACompleteNoOp_RegardlessOfDayOrPhase()
    {
        Assert.Equal(TribeVoteCalendarTransition.None,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Closed, dayOfMonth: 1, hourOfDay: 0, testMode: true));
        Assert.Equal(TribeVoteCalendarTransition.None,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Voting, dayOfMonth: 15, hourOfDay: 12, testMode: true));
    }

    [Fact]
    public void HypotheticalNonCalendarDay_BelowOne_StructurallyReachesTheOtherwiseBranches()
    {
        // Documents the legacy's own day<=2 cascade structure -- unreachable with a real DateTime.Day (always
        // 1-31), included only to prove the branch precedence is faithfully modeled. The day<=3 branch and the
        // final fallback below it are NOT exercised here (unlike this pair): per Evaluate's own remarks, they
        // require day>=1 to be false (to fall past the first branch) AND day>2 (to fall past day<=2) at the
        // same time, which is self-contradictory -- there is no int value, real calendar day or otherwise,
        // that can ever reach them.
        Assert.Equal(TribeVoteCalendarTransition.OpenVoting,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Closed, dayOfMonth: 0, hourOfDay: 0, testMode: false));
        Assert.Equal(TribeVoteCalendarTransition.OpenVoting,
            TribeVoteElectionCalendar.Evaluate(TribeVotePhase.Closed, dayOfMonth: -1, hourOfDay: 0, testMode: false));
    }
}
