namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     A phase-transition notification emitted by <see cref="Zone175MissionCore" /> to
///     <see cref="IZone175MissionEffects.Notify" />. In the legacy each of these corresponds to a
///     center-directed broadcast (drives an on-screen announcement AND instructs the center which state value to
///     write next) or a mission-lifecycle log record; in Fenrir's two-executable topology there is no receiving
///     center process, so the real effects implementation collapses these to structured log lines -- the same
///     collapse <c>Zone.AnnounceEliteBossDefeated</c> already uses for its own Center broadcast hop.
/// </summary>
/// <remarks>
///     Réf. C++ : <c>Server/ts25zone/S07_MyGame01.cpp:8785</c> (mission-start log, <see cref="MissionOpen" />),
///     <c>:9286</c> (mission-end log, <see cref="MissionEnd" />), <c>:8797,:8811,:8822-9308</c> (per-phase
///     broadcasts); center receipt/next-state writes at <c>Server/ts25center/S04_MyWork02.cpp:611-802</c>.
/// </remarks>
public enum Zone175MissionEvent : byte
{
    /// <summary>Mission opened (Sunday 21:00): also the "mission start" log record.</summary>
    MissionOpen,

    /// <summary>One pre-open countdown tick -- carries the remaining count.</summary>
    PreOpenCountdown,

    /// <summary>A wave's gate-open phase.</summary>
    WaveGateOpen,

    /// <summary>A wave's boss-summon phase.</summary>
    WaveBossSummon,

    /// <summary>A wave was cleared (its wave-boss special type no longer alive); the reward routine has run.</summary>
    WaveCleared,

    /// <summary>A wave aborted because no qualifying player was present.</summary>
    EmptyAbort,

    /// <summary>A wave aborted on the fixed 60-minute combat timeout.</summary>
    WaveTimeout,

    /// <summary>An inter-wave depth gate (<c>index2</c>) stopped progression short of wave 5.</summary>
    DepthGateStop,

    /// <summary>The fifth wave was cleared: also the "mission end" log record.</summary>
    MissionEnd,

    /// <summary>Entered the terminal hold (legacy state 23).</summary>
    TerminalEnter,

    /// <summary>Terminal hold elapsed: every player was force-disconnected and the mission reset to idle.</summary>
    TerminalKickReset
}
