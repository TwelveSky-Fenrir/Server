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
        _ = currentPhase;
        _ = dayOfMonth;
        _ = hourOfDay;
        _ = testMode;
        return TribeVoteCalendarTransition.None;
    }
}
