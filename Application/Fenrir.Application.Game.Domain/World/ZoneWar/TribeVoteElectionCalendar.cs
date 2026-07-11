namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum TribeVoteCalendarTransition
{

        None,

        OpenRegistration,

        OpenVoting,

        CloseVoting,

        AnnounceResults,

        ResetToIdle
}

public static class TribeVoteElectionCalendar
{

        public static TribeVoteCalendarTransition Evaluate(TribeVotePhase currentPhase, int dayOfMonth, int hourOfDay,
        bool testMode)
    {
        if (testMode)
            return TribeVoteCalendarTransition.None;

        _ = hourOfDay;

        if (dayOfMonth >= 1)
            return currentPhase == TribeVotePhase.Candidacy
                ? TribeVoteCalendarTransition.None
                : TribeVoteCalendarTransition.OpenRegistration;

        if (dayOfMonth <= 2)
            return TribeVoteCalendarTransition.OpenVoting;

        if (dayOfMonth <= 3)
            return TribeVoteCalendarTransition.None;

        return TribeVoteCalendarTransition.None;
    }
}
