namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     The tick-driven state machine <c>Zone.AdvanceZone241PersonalDungeonInstances</c> steps a Zone-241 "LOD"
///     personal-boss-chain avatar through, one server tick at a time, while the owning <see cref="Zone" /> is
///     configured as Zone-241-type (<see cref="Zone.IsZone241TypeZone" />).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11763-12013 (the active, <c>USE_INSTANCE</c>-gated
///     <c>Process_Zone_241_TYPE()</c> implementation -- states 0-8 named there).
///     <para>
///         Only <see cref="Idle" />, <see cref="Summoning" />, <see cref="BattleInProgress" />, and
///         <see cref="Success" /> have a driven transition in this codebase today -- see
///         <c>Zone.DungeonInstance.cs</c>'s own remarks for exactly which states/transitions were left out
///         and why (the contract this was implemented from does not specify <see cref="Failure" />'s trigger
///         condition, nor the <see cref="WaitBeforeReturn" />/<see cref="ReturnToTownNotice" />/
///         <see cref="DisconnectPending" /> tick durations, and inventing those numbers would violate the "no
///         legacy parity from memory" rule). <see cref="Waiting" /> is never entered at all: the one arming
///         call site (the zone-enter handler) always sets <see cref="Summoning" /> directly.
///     </para>
/// </remarks>
public enum DungeonInstanceLifecycle
{
    /// <summary>Legacy 0 -- the post-<c>Init()</c>/<c>Free()</c> default (Trigger 1 -- connection-slot alloc/free).</summary>
    Idle = 0,

    /// <summary>Legacy 1. Never produced by this implementation -- see class remarks.</summary>
    Waiting = 1,

    /// <summary>Legacy 2 -- set unconditionally by the zone-enter handler the instant it re-arms.</summary>
    Summoning = 2,

    /// <summary>Legacy 3 -- a status broadcast fires every 20 ticks while an avatar sits in this state.</summary>
    BattleInProgress = 3,

    /// <summary>Legacy 4 -- the personal boss is dead.</summary>
    Success = 4,

    /// <summary>Legacy 5. No driven trigger in this implementation -- see class remarks.</summary>
    Failure = 5,

    /// <summary>Legacy 6. No driven transition in this implementation -- see class remarks.</summary>
    WaitBeforeReturn = 6,

    /// <summary>Legacy 7. No driven transition in this implementation -- see class remarks.</summary>
    ReturnToTownNotice = 7,

    /// <summary>Legacy 8. No driven transition in this implementation -- see class remarks.</summary>
    DisconnectPending = 8
}

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     <c>DUNGEON_INSTANCE::mID</c> -- the avatar's own connection-slot index while an instance is armed,
    ///     null otherwise. Fenrir has no small per-connection slot counter distinct from
    ///     <see cref="CharacterId" />, so this always equals <see cref="CharacterId" /> once armed (the same
    ///     convention <see cref="Session" />-adjacent code already uses <see cref="CharacterId" /> as the
    ///     legacy <c>mIndex</c> stand-in, e.g. every <c>ServerIndex</c> in the avatar broadcast packets).
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/H07_MyGame.h:24-41 (<c>DUNGEON_INSTANCE</c> shape).</remarks>
    public int? DungeonInstanceId { get; set; }

    /// <summary><c>DUNGEON_INSTANCE::mState</c> -- see <see cref="DungeonInstanceLifecycle" />.</summary>
    public DungeonInstanceLifecycle DungeonInstanceLifecycleState { get; set; } = DungeonInstanceLifecycle.Idle;

    /// <summary>
    ///     <c>DUNGEON_INSTANCE::mTick</c> -- elapsed ticks since the instance was last armed; reset to 0 by
    ///     <c>Zone.TryEnterZone241PersonalInstance</c>, incremented once per tick by
    ///     <c>Zone.AdvanceZone241PersonalDungeonInstances</c> while non-<see cref="DungeonInstanceLifecycle.Idle" />.
    /// </summary>
    public int DungeonInstanceTick { get; set; }

    /// <summary>
    ///     The per-character "rounds remaining" quota gating entry (Server/ts25zone/S07_MyGame03.cpp:6272-6278):
    ///     an avatar below 1 here is refused entry outright, no instance state touched. Consumed by one on
    ///     every successful grant.
    /// </summary>
    /// <remarks>
    ///     Not yet sourced from a persisted column: no <c>game.Characters</c> quota field or stored procedure
    ///     for it exists yet (out of scope for this pass -- needs a fenrir-database-engineer-authored column +
    ///     procedure), so this always starts at 0 on a fresh login/zone-transfer and the gate below is
    ///     therefore always closed in production today. The gate itself, and the decrement-on-grant behavior,
    ///     are real and directly testable by setting this property before calling
    ///     <c>Zone.TryEnterZone241PersonalInstance</c> -- same "real API, currently unreachable in production"
    ///     posture as e.g. <see cref="AnimalTime" />.
    /// </remarks>
    public int DungeonInstanceRoundsRemaining { get; set; }
}
