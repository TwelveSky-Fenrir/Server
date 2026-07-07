namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Legacy-tick accumulator for <see cref="Simulation.HoisundoCountdownSystem" />'s own once-per-real-minute
    ///     cadence -- never read by anything else, same role as <see cref="PlayTimeAccrualTicks" />/
    ///     <see cref="PetActivityDecayTicks" />.
    /// </summary>
    public int HoisundoAccrualTicks { get; set; }

    /// <summary>
    ///     aZoneNNNTime (N = 234..240) -- the "Hoisundo" forced-departure countdown for the rebirth-event zones
    ///     234-240 (<c>ZONE234</c>-<c>ZONE241</c>, <c>#ifdef __REBIRTH__</c>, always active). One field stands in
    ///     for legacy's seven distinct per-avatar columns (<c>aZone234Time</c>..<c>aZone240Time</c>,
    ///     Server/Header/Protocol/STRUCT.h:419-425) since at most one of the seven is ever "live" for a single
    ///     connected avatar -- like every other per-zone-entry field on this type, a fresh
    ///     <see cref="PlayerRuntimeState" /> is built on every <see cref="Zone.HandleEnter" /> and discarded on
    ///     every <see cref="Zone.HandleLeave" />, so transferring between two zones in 234-240 naturally starts a
    ///     fresh countdown rather than carrying one field's value into a different field's slot (same "fresh
    ///     object, no cross-entry carry" posture <c>PlayerRuntimeState.DungeonInstance.cs</c>'s own remarks
    ///     document for Zone-241's per-avatar instance state).
    /// </summary>
    /// <remarks>
    ///     Not yet sourced from a persisted column, nor from the legacy "zone key" item-consumption mechanic that
    ///     grants/extends it (Server/ts25zone/S04_MyWork03.cpp:6130-6237, Server/ts25zone/S07_MyGame04.cpp:5697-5806)
    ///     -- neither is covered by any behavior contract this pass was built from, and guessing either would
    ///     violate the "no legacy parity from memory" rule. Until a follow-up contract supplies one or both, this
    ///     always starts at its C# default (0) on a fresh zone entry, so
    ///     <see cref="Simulation.HoisundoCountdownSystem" /> disconnects any avatar it finds in a 234-240 zone at
    ///     the very first real-minute boundary -- the same "real API, currently starts pre-exhausted in
    ///     production" posture <see cref="DungeonInstanceRoundsRemaining" />'s own remarks already document for
    ///     its sibling not-yet-persisted quota field.
    /// </remarks>
    public int HoisundoTimeRemaining { get; set; }
}
