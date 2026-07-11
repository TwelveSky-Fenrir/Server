using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers <see cref="Zone124MassDuelState" /> -- the C-supporting zone-wide mass-duel countdown of the B10
///     contract: start (60), decrement/broadcast per qualifying tick, early empty-team teardown, and the
///     headcount winner at the final unit (<c>Process_Zone_124_TYPE</c>,
///     <c>Server/ts25zone/S07_MyGame01.cpp:4351-4494</c>; <c>S04_MyWork04.cpp:1879-2013</c>).
/// </summary>
public class Zone124MassDuelStateTests
{
    [Fact]
    public void Start_SeedsCountdownAndHeadcounts_AndRaisesActive()
    {
        var state = new Zone124MassDuelState();
        state.Start(team1Count: 4, team2Count: 3);

        Assert.True(state.Active);
        Assert.Equal(Zone124MassDuelState.StartUnits, state.RemainingUnits);
        Assert.Equal(60, state.RemainingUnits);
        Assert.Equal(4, state.Team1Count);
        Assert.Equal(3, state.Team2Count);
    }

    [Fact]
    public void Advance_WhenIdle_ReturnsIdle_NoStateChange()
    {
        var state = new Zone124MassDuelState();

        var step = state.Advance();

        Assert.Equal(Zone124CountdownAction.Idle, step.Action);
        Assert.False(state.Active);
        Assert.Equal(0, state.RemainingUnits);
    }

    [Fact]
    public void Advance_DecrementsByOne_PerQualifyingTick()
    {
        var state = new Zone124MassDuelState();
        state.Start(2, 2);

        var step = state.Advance(); // 60 -> 59

        Assert.Equal(Zone124CountdownAction.Decremented, step.Action);
        Assert.Equal(59, step.RemainingUnits);
        Assert.Equal(59, state.RemainingUnits);
    }

    [Fact]
    public void Advance_BroadcastTime_OnFiveUnitCadence()
    {
        var state = new Zone124MassDuelState();
        state.Start(2, 2); // 60

        // First decrement lands on 59 (not a multiple of 5) -> no broadcast.
        Assert.Equal(Zone124CountdownAction.Decremented, state.Advance().Action);

        // Advance down to 55, which IS a multiple of 5 -> broadcast due.
        Zone124CountdownStep step = default;
        for (var i = 0; i < 4; i++) // 59 -> 58 -> 57 -> 56 -> 55
            step = state.Advance();

        Assert.Equal(55, step.RemainingUnits);
        Assert.Equal(Zone124CountdownAction.DecrementedBroadcastTime, step.Action);
    }

    [Fact]
    public void Advance_AtFinalUnit_ConcludesWithHeadcountWinner_AndResets()
    {
        var state = new Zone124MassDuelState();
        state.Start(5, 2);
        state.SetTeamCounts(3, 1); // team one leads
        DriveDownTo(state, 1);

        var step = state.Advance();

        Assert.Equal(Zone124CountdownAction.Concluded, step.Action);
        Assert.Equal(Zone124DuelWinner.Team1, step.Winner);
        Assert.False(state.Active); // torn down
        Assert.Equal(0, state.RemainingUnits);
    }

    [Fact]
    public void Advance_AtFinalUnit_EqualHeadcounts_IsDraw()
    {
        var state = new Zone124MassDuelState();
        state.Start(2, 2);
        DriveDownTo(state, 1);

        var step = state.Advance();

        Assert.Equal(Zone124CountdownAction.Concluded, step.Action);
        Assert.Equal(Zone124DuelWinner.Draw, step.Winner);
    }

    [Fact]
    public void Advance_AtFinalUnit_Team2Leads_IsTeam2()
    {
        var state = new Zone124MassDuelState();
        state.Start(2, 2);
        state.SetTeamCounts(1, 4);
        DriveDownTo(state, 1);

        Assert.Equal(Zone124DuelWinner.Team2, state.Advance().Winner);
    }

    [Fact]
    public void Advance_EitherTeamEmpty_TearsDownEarly_NoWinner()
    {
        var state = new Zone124MassDuelState();
        state.Start(3, 2);
        state.SetTeamCounts(3, 0); // team two wiped mid-event

        var step = state.Advance();

        Assert.Equal(Zone124CountdownAction.TornDownEmptyTeam, step.Action);
        Assert.Equal(Zone124DuelWinner.Draw, step.Winner); // empty-case result indicator is unspecified -> neutral
        Assert.False(state.Active);
        Assert.Equal(0, state.RemainingUnits);
    }

    [Fact]
    public void Advance_EmptyTeamCheck_WinsEvenOnTheFinalUnit()
    {
        var state = new Zone124MassDuelState();
        state.Start(3, 2);
        DriveDownTo(state, 1);
        state.SetTeamCounts(3, 0); // at the final unit, but a team is empty

        // Empty-teardown is checked before the countdown, so it takes precedence over Concluded.
        Assert.Equal(Zone124CountdownAction.TornDownEmptyTeam, state.Advance().Action);
    }

    [Fact]
    public void Reset_ZeroesEverything()
    {
        var state = new Zone124MassDuelState();
        state.Start(4, 4);
        state.Reset();

        Assert.False(state.Active);
        Assert.Equal(0, state.RemainingUnits);
        Assert.Equal(0, state.Team1Count);
        Assert.Equal(0, state.Team2Count);
    }

    [Fact]
    public void DecideWinner_ComparesHeadcountsPurely()
    {
        var state = new Zone124MassDuelState();
        state.SetTeamCounts(5, 5);
        Assert.Equal(Zone124DuelWinner.Draw, state.DecideWinner());
        state.SetTeamCounts(6, 5);
        Assert.Equal(Zone124DuelWinner.Team1, state.DecideWinner());
        state.SetTeamCounts(5, 6);
        Assert.Equal(Zone124DuelWinner.Team2, state.DecideWinner());
    }

    /// <summary>Applies qualifying ticks until the countdown reads <paramref name="target" />, without concluding.</summary>
    private static void DriveDownTo(Zone124MassDuelState state, int target)
    {
        while (state.RemainingUnits > target)
            state.Advance();
    }
}
