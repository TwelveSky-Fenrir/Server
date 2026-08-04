namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class Zone175MissionState
{
    public Zone175MissionPhase Phase { get; set; } = Zone175MissionPhase.Idle;

    public int SharedState { get; set; } = -1;

    public int StateTicks { get; set; }

    public int SubTick { get; set; }

    public int IdleBattleState { get; set; }

    public int CountdownRemaining { get; set; }

    public DateOnly? LastScheduledDateLocal { get; set; }

    public int LoadedStage { get; set; }

    public bool StageLoaded { get; set; }

    public bool StageLoadBlocked { get; set; }
}
