namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     The single shared mission-state cell one Zone175-type process owns (legacy's per-process
///     <c>DUNGEON</c>-style struct behind the shared world-info cell). One instance per hosted Zone175 map,
///     built lazily by <see cref="Zone175LabyrinthSystem" /> and mutated only on that zone's own tick thread
///     (single-writer, same contract as every other per-zone mutable state in this codebase).
/// </summary>
/// <remarks>
///     All counters are plain <see cref="int" /> legacy-tick counters (no per-tick allocation): the machine
///     accumulates elapsed legacy ticks and acts on whole-cadence boundaries, carrying the remainder forward,
///     the same shape <see cref="PlayTimeAccrualSystem" />/<see cref="StunCountdownSystem" /> use.
/// </remarks>
public sealed class Zone175MissionState
{
    /// <summary>Current lifecycle phase (Fenrir-authoritative; see <see cref="Zone175MissionPhase" />).</summary>
    public Zone175MissionPhase Phase { get; set; } = Zone175MissionPhase.Idle;

    /// <summary>Current 1-based wave (1-5) while a wave phase is active; 0 while idle/pre-open.</summary>
    public int CurrentWave { get; set; }

    /// <summary>The mission's internal sub-tick counter (legacy <c>mTickCount</c>), advanced every legacy tick.</summary>
    public int SubTick { get; set; }

    /// <summary>Remaining pre-open countdown value (starts at 10, decremented once per one-minute cadence).</summary>
    public int PreOpenRemaining { get; set; }

    /// <summary>
    ///     General-purpose per-phase legacy-tick accumulator: pre-open countdown cadence, per-wave combat
    ///     timeout, and terminal hold all measure against this (reset on entering each phase).
    /// </summary>
    public int PhaseAccumulatorTicks { get; set; }

    /// <summary>Legacy-tick accumulator toward the next 20-sub-tick trickle summon during combat.</summary>
    public int TrickleAccumulatorTicks { get; set; }

    /// <summary>
    ///     The UTC calendar date the mission last opened, so the Sunday-21:00 gate opens at most once per matching
    ///     minute (the gate is polled on every idle tick, of which there are many within the 21:00 minute).
    /// </summary>
    public DateOnly? LastOpenedDateUtc { get; set; }
}
