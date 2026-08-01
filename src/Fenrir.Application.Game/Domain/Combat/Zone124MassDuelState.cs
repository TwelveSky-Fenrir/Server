namespace Fenrir.Application.Game.Domain.Combat;

public enum Zone124DuelWinner : byte
{
    Draw,

    Team1,

    Team2
}

public enum Zone124CountdownAction : byte
{
    Idle,

    Decremented,

    DecrementedBroadcastTime,

    TornDownEmptyTeam,

    Concluded
}

public readonly record struct Zone124CountdownStep(
    Zone124CountdownAction Action,
    int RemainingUnits,
    Zone124DuelWinner Winner);

public sealed class Zone124MassDuelState
{
    public const int StartUnits = 60;

    public const int TimeBroadcastCadenceUnits = 5;

    public bool Active { get; private set; }

    public int RemainingUnits { get; private set; }

    public int Team1Count { get; private set; }

    public int Team2Count { get; private set; }

    public void Start(int team1Count, int team2Count)
    {
        Active = true;
        RemainingUnits = StartUnits;
        Team1Count = team1Count;
        Team2Count = team2Count;
    }

    public void Reset()
    {
        Active = false;
        RemainingUnits = 0;
        Team1Count = 0;
        Team2Count = 0;
    }

    public void SetTeamCounts(int team1Count, int team2Count)
    {
        Team1Count = team1Count;
        Team2Count = team2Count;
    }

    public Zone124CountdownStep Advance()
    {
        if (!Active)
            return new Zone124CountdownStep(Zone124CountdownAction.Idle, 0, Zone124DuelWinner.Draw);

        if (Team1Count == 0 || Team2Count == 0)
        {
            Reset();
            return new Zone124CountdownStep(Zone124CountdownAction.TornDownEmptyTeam, 0, Zone124DuelWinner.Draw);
        }

        if (RemainingUnits <= 1)
        {
            var winner = DecideWinner();
            Reset();
            return new Zone124CountdownStep(Zone124CountdownAction.Concluded, 0, winner);
        }

        RemainingUnits--;
        var broadcastDue = RemainingUnits % TimeBroadcastCadenceUnits == 0;
        return new Zone124CountdownStep(
            broadcastDue ? Zone124CountdownAction.DecrementedBroadcastTime : Zone124CountdownAction.Decremented,
            RemainingUnits,
            Zone124DuelWinner.Draw);
    }

    public Zone124DuelWinner DecideWinner()
    {
        if (Team1Count == Team2Count)
            return Zone124DuelWinner.Draw;
        return Team1Count > Team2Count ? Zone124DuelWinner.Team1 : Zone124DuelWinner.Team2;
    }
}
