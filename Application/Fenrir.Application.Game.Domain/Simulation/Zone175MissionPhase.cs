namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Fenrir-side lifecycle phase for the Zone175 "Labyrinth" 5-wave PvE mission. This is a
///     <em>Fenrir-authoritative</em> re-expression of the legacy's numbered state-cell machine (state codes
///     0-23), NOT a 1:1 port of those raw codes.
/// </summary>
/// <remarks>
///     Only two of the 24 legacy state codes are independently grounded by the source behavior contract:
///     code <b>0</b> = idle (<see cref="Idle" />) and code <b>23</b> = terminal (<see cref="Terminal" />)
///     (<c>Server/ts25zone/S07_MyGame01.cpp:8758,9288-9308</c>). The intermediate codes (1-22) are
///     <em>center-driven</em>: the zone broadcasts a phase code and the center writes the next state value back
///     into the shared cell, so "the precise next-state mapping lives in the center handler, not in this
///     routine" (<c>Server/ts25center/S04_MyWork02.cpp:611-802</c>). That per-code mapping is a documented GAP
///     in the Fenrir center merge (see <c>ZoneWar.ZoneCenterBroadcastIngestor</c> / <c>ZoneCenterSiegeState</c>
///     Zone175 remarks -- it stores the raw event code as a placeholder). Rather than invent the 1-22 code
///     semantics, this machine keeps its own small, self-authoritative phase set and drives the transitions
///     locally on the zone tick -- the same "keep the instance state local rather than round-trip through the
///     center" divergence <c>Zone.DungeonInstance.cs</c> (Zone241) already makes, flagged there too.
/// </remarks>
public enum Zone175MissionPhase : byte
{
    /// <summary>Legacy state 0. Waiting for the Sunday-21:00 open moment.</summary>
    Idle = 0,

    /// <summary>Pre-open countdown (value 10, decremented once per one-minute cadence) before wave 1 begins.</summary>
    PreOpen = 1,

    /// <summary>
    ///     A wave's boss-summon phase: the wave boss (special type 40-44, one per wave) is summoned, then the
    ///     machine advances to <see cref="WaveCombat" />. The contract's separate "gate-open" step is emitted as
    ///     an event (<see cref="Zone175MissionEvent.WaveGateOpen" />) rather than modeled as its own dwell phase.
    /// </summary>
    WaveBossSummon = 2,

    /// <summary>
    ///     A wave's combat phase: presence scan (empty-abort), 60-minute timeout-abort, 20-sub-tick trickle
    ///     summon, and the wave-boss clear check that triggers the reward routine.
    /// </summary>
    WaveCombat = 3,

    /// <summary>
    ///     Legacy state 23. Reached on <em>every</em> mission end (5-wave completion, empty-abort, timeout, and
    ///     depth-gate stop all funnel here): a fixed 60-minute hold, then force-disconnect every player and reset
    ///     to <see cref="Idle" />.
    /// </summary>
    Terminal = 23
}
