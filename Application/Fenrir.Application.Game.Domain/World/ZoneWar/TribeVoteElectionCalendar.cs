namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>Which of TRIBE_WORK tSort 52-56's transitions a calendar tick should apply, if any.</summary>
public enum TribeVoteCalendarTransition
{
    /// <summary>No calendar bucket boundary was crossed this tick -- do nothing.</summary>
    None,

    /// <summary>tSort 52: open candidacy for all 4 tribes and wipe every candidate slot.</summary>
    OpenRegistration,

    /// <summary>tSort 53: close candidacy and open voting for all 4 tribes.</summary>
    OpenVoting,

    /// <summary>tSort 54: close voting for all 4 tribes without tallying.</summary>
    CloseVoting,

    /// <summary>tSort 55: tally every tribe's Force Leader and clear every tribe's sub-leader roster.</summary>
    AnnounceResults,

    /// <summary>tSort 56: reset every tribe back to idle and wipe every candidate slot.</summary>
    ResetToIdle
}

/// <summary>
///     Pure day/hour decision function for <c>Process_TribeVote_Server</c> -- deliberately reproduces the
///     legacy's own always-true first branch rather than "fixing" it, since that bug is the actually-observed
///     production behavior of every live server number 37 instance. Takes the current calendar day-of-month/
///     hour-of-day as plain parameters (never reads the clock itself) so every branch, including the ones a
///     real <see cref="System.DateTime" /> can never reach, is directly unit-testable.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3530-3630 (<c>Process_TribeVote_Server</c>) -- the
///     <c>day &gt;= 1</c> / <c>day &lt;= 2</c> / <c>day &lt;= 3</c> cascade and the entirely commented-out
///     <c>mVoteTribeTest</c> branch (lines 3583-3629) ; Server/ts25zone/S07_MyGame01.cpp:84-93
///     (<c>UpdateServerTime</c>) and Server/Header/datetime.h:71-73 confirm day/hour come from the real system
///     clock's <c>tm_mday</c>/<c>tm_hour</c>, refreshed every tick, never a virtual/in-game counter.
///     <para>
///         EDGE CASE (preserved, not fixed): <see cref="System.DateTime.Day" /> is always 1-31, so the
///         <c>day &gt;= 1</c> branch is always taken and the <c>day &lt;= 2</c> / <c>day &lt;= 3</c> branches
///         below it can never run on a live server -- <see cref="TribeVoteCalendarTransition.OpenVoting" />/
///         <see cref="TribeVoteCalendarTransition.CloseVoting" />/<see cref="TribeVoteCalendarTransition.AnnounceResults" />/
///         <see cref="TribeVoteCalendarTransition.ResetToIdle" /> are therefore never automatically reachable
///         through this function; only <see cref="TribeVoteCalendarTransition.OpenRegistration" /> (guarded by
///         "already registration-open, do nothing") and <see cref="TribeVoteCalendarTransition.None" /> are.
///         The observed consequence in production: the very first tick this ever runs opens candidacy, and
///         the cycle stays open-for-candidacy forever -- voting never opens, no candidate is ever tallied, and
///         no Force Leader is ever appointed by this automatic path.
///     </para>
///     <para>
///         GAP: the legacy's <c>day &lt;= 3</c> hour sub-branch (which would pick between close-voting/
///         announce-results/reset-to-idle) has no cited numeric hour thresholds in this behavior's translated
///         contract, and can only ever run when <paramref name="dayOfMonth" /> is 0 or negative -- impossible
///         for a real calendar day -- so no choice of threshold here could ever change production behavior.
///         Left unimplemented rather than guessed at; <see cref="TribeVoteCalendarTransition.CloseVoting" />/
///         <see cref="TribeVoteCalendarTransition.AnnounceResults" />/<see cref="TribeVoteCalendarTransition.ResetToIdle" />
///         are real, correctly-modeled transitions on <c>TribeVoteElection</c> that a future citation of the
///         exact hour cut points could wire this function up to emit automatically.
///     </para>
/// </remarks>
public static class TribeVoteElectionCalendar
{
    /// <param name="currentPhase">The election cycle's phase as of the start of this tick.</param>
    /// <param name="dayOfMonth">Real calendar day-of-month, 1-31 for any genuine <see cref="System.DateTime" />.</param>
    /// <param name="hourOfDay">Real calendar hour-of-day, 0-23.</param>
    /// <param name="testMode">
    ///     The instance's <c>VoteTribeTest</c> flag -- when true, mirrors the legacy's entirely commented-out
    ///     test branch: always <see cref="TribeVoteCalendarTransition.None" />, regardless of every other input.
    /// </param>
    public static TribeVoteCalendarTransition Evaluate(TribeVotePhase currentPhase, int dayOfMonth, int hourOfDay,
        bool testMode)
    {
        if (testMode)
            return TribeVoteCalendarTransition.None;

        _ = hourOfDay; // only the unreachable day<=3 branch below would ever consult this -- see remarks.

        if (dayOfMonth >= 1)
            return currentPhase == TribeVotePhase.Candidacy
                ? TribeVoteCalendarTransition.None
                : TribeVoteCalendarTransition.OpenRegistration;

        if (dayOfMonth <= 2) // unreachable: a real DateTime.Day is always >= 1 -- see remarks.
            return TribeVoteCalendarTransition.OpenVoting;

        if (dayOfMonth <= 3) // unreachable: a real DateTime.Day is always >= 1 -- see remarks (hour gap).
            return TribeVoteCalendarTransition.None;

        return TribeVoteCalendarTransition.None;
    }
}
