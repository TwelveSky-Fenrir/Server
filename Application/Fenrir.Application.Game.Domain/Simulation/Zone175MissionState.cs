namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class Zone175MissionState
{
    public Zone175MissionPhase Phase { get; set; } = Zone175MissionPhase.Idle;

    public int CurrentWave { get; set; }

    public int SubTick { get; set; }

    public int PreOpenRemaining { get; set; }

    public int PhaseAccumulatorTicks { get; set; }

    public int TrickleAccumulatorTicks { get; set; }

    public DateOnly? LastOpenedDateUtc { get; set; }
}
