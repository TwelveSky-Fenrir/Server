using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

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

        var step = state.Advance();

        Assert.Equal(Zone124CountdownAction.Decremented, step.Action);
        Assert.Equal(59, step.RemainingUnits);
        Assert.Equal(59, state.RemainingUnits);
    }

    [Fact]
    public void Advance_BroadcastTime_OnFiveUnitCadence()
    {
        var state = new Zone124MassDuelState();
        state.Start(2, 2);

        Assert.Equal(Zone124CountdownAction.Decremented, state.Advance().Action);

        Zone124CountdownStep step = default;
        for (var i = 0; i < 4; i++)
            step = state.Advance();

        Assert.Equal(55, step.RemainingUnits);
        Assert.Equal(Zone124CountdownAction.DecrementedBroadcastTime, step.Action);
    }

    [Fact]
    public void Advance_AtFinalUnit_ConcludesWithHeadcountWinner_AndResets()
    {
        var state = new Zone124MassDuelState();
        state.Start(5, 2);
        state.SetTeamCounts(3, 1);
        DriveDownTo(state, 1);

        var step = state.Advance();

        Assert.Equal(Zone124CountdownAction.Concluded, step.Action);
        Assert.Equal(Zone124DuelWinner.Team1, step.Winner);
        Assert.False(state.Active);
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
        state.SetTeamCounts(3, 0);

        var step = state.Advance();

        Assert.Equal(Zone124CountdownAction.TornDownEmptyTeam, step.Action);
        Assert.Equal(Zone124DuelWinner.Draw, step.Winner);
        Assert.False(state.Active);
        Assert.Equal(0, state.RemainingUnits);
    }

    [Fact]
    public void Advance_EmptyTeamCheck_WinsEvenOnTheFinalUnit()
    {
        var state = new Zone124MassDuelState();
        state.Start(3, 2);
        DriveDownTo(state, 1);
        state.SetTeamCounts(3, 0);

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

        private static void DriveDownTo(Zone124MassDuelState state, int target)
    {
        while (state.RemainingUnits > target)
            state.Advance();
    }
}
