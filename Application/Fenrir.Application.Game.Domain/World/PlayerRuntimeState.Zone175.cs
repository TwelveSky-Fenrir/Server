namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Zone175 "Labyrinth" per-player boss cumulative-damage accumulator (legacy
    ///     <c>Server/ts25zone/H07_MyGame.h:874</c>). A <b>display-only</b> running total of the actual damage
    ///     this player has dealt to the current wave boss in the shipped ReleaseEU33 (<c>LNW33</c>) build -- it
    ///     never gates or scales any reward (every qualifying present player gets the full fixed reward
    ///     regardless of contribution). Reset to 0 by the wave-clear reward routine
    ///     (<c>Zone.GrantZone175WaveReward</c>) and, on load, automatically -- every zone entry constructs a fresh
    ///     <see cref="PlayerRuntimeState" />, matching legacy's own reset at <c>S03_MyUser.cpp:430</c>.
    /// </summary>
    /// <remarks>
    ///     DEFERRED: the per-hit accumulation itself (each hit on a wave-boss monster of special type 40-44 adds
    ///     the damage dealt and echoes the running total back on the attack result,
    ///     <c>Server/ts25zone/S07_MyGame02.cpp:2394-2416</c>) is a combat-hit-path side effect and belongs to
    ///     <c>fenrir-gameplay-domain-engineer</c>'s combat resolution, not this tick-loop scaffolding. This field
    ///     is added here so the reset half (owned by the mission) has a home and so the feed can be wired without
    ///     a further schema change. It stays 0 until that combat feed exists.
    /// </remarks>
    public long Zone175BossDamage { get; set; }
}
