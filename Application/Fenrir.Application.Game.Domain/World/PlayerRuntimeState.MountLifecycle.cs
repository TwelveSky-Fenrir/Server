using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Mount lifecycle / progression fields that the core mount state (garage, index, number, absorb, time,
///     per-slot experience/rolled-attribute arrays) on <c>PlayerRuntimeState.Companions.cs</c> does not already
///     carry. Every field here is mutated only from this character's own zone tick thread -- the same
///     single-writer contract every other <see cref="PlayerRuntimeState" /> field relies on. See
///     <see cref="Mounts.MountKillExperienceCalculator" /> / <see cref="Simulation.MountExpiryCountdownSystem" />
///     for the systems that read them.
/// </summary>
/// <remarks>
///     Companions.cs deliberately kept the two halves of the legacy <c>aAnimalExpActivity</c> packed integer
///     split: it already stores the per-slot decoded experience (<see cref="MountAccumulatedExp" />), but not
///     the activity half. <see cref="MountActivity" /> supplies that missing half, so the pair can be re-encoded
///     into the persisted <c>game.Characters.MountExpActivity</c> column via
///     <see cref="Mounts.MountActivityExpCodec" /> on the rare persistence write, and decoded back on world
///     entry. Same "session-scoped, no dedicated grant/hydration path wired yet, starts at its C# default in
///     production" posture the rest of the mount family documents on Companions.cs.
/// </remarks>
public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Per-garage-slot mount activity ("absorb" fuel), 0-<see cref="MountActivityExpCodec.MaxActivity" /> --
    ///     the activity half of the legacy <c>aAnimalExpActivity[slot]</c> packed integer, kept decoded here (the
    ///     experience half is <see cref="MountAccumulatedExp" />). Raised only by feeding a mount-activity potion
    ///     (op22 potion type 16, via <see cref="MountActivityExpCodec.FeedActivity" />). Gates inter-tribe-kill
    ///     mount experience: a slot at activity 0 never accrues kill experience (see
    ///     <see cref="Mounts.MountKillExperienceCalculator" />). Same "no acquisition/feed path wired yet -> every
    ///     slot stays 0 in production" posture as <see cref="MountGarage" />.
    /// </summary>
    public ImmutableArray<int> MountActivity { get; set; } = ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    ///     The session's mount-XP-up flag -- when set, inter-tribe-kill mount experience is doubled a second time
    ///     (on top of the <see cref="AnimalDoubleExp" /> character-counter doubling), per
    ///     Server/ts25zone/S07_MyGame03.cpp:3190-3210. Session-scoped only; sourced in legacy from a per-session
    ///     buff/flag with no persisted column, so it defaults to false until a grant path is wired -- same
    ///     posture as <see cref="MountActivity" />.
    /// </summary>
    public bool MountExpUp { get; set; }

    /// <summary>
    ///     Legacy-tick accumulator for <see cref="Simulation.MountExpiryCountdownSystem" />'s once-per-real-minute
    ///     cadence -- deliberately a separate counter from the other per-minute systems'
    ///     (<see cref="PlayTimeAccrualTicks" />/<see cref="PetExpX2TimeAccrualTicks" />/
    ///     <see cref="TimedBuffCountdownAccrualTicks" />), for the same reason each of those keeps its own:
    ///     sharing one accumulator across independently-owned per-minute systems would double-consume/desync them.
    /// </summary>
    public int MountExpiryCountdownAccrualTicks { get; set; }

    /// <summary>
    ///     Latched true by <see cref="Simulation.MountExpiryCountdownSystem" /> (or the world-registration path)
    ///     when an expired mount was force-dismounted, so the zone's own post-systems stage can broadcast the
    ///     dismount appearance change and recompute the character's ability/HP-MP -- the same deferred hand-off
    ///     shape <see cref="PaidZoneEvictionPending" /> uses for eviction, rather than reaching into the zone's
    ///     broadcast internals from inside the self-contained simulation system. Never cleared here -- a latch the
    ///     consumer acts on once.
    /// </summary>
    public bool MountAutoDismountPending { get; set; }
}
