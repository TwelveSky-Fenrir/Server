namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Per-real-minute timed-buff / scroll / experience-boost countdown fields plus the paid-zone occupancy
///     counters consumed by <see cref="Simulation.TimedBuffCountdownSystem" /> (behavior contract "Timed-buff /
///     scroll countdown + paid-zone occupancy eviction", Server/ts25zone/S07_MyGame04.cpp:913-1119). Every
///     field here is a whole-number remaining-time counter floored at 0 (minutes, except the accumulator),
///     mutated only from this character's own zone tick thread -- the same single-writer contract every other
///     <see cref="PlayerRuntimeState" /> field relies on.
/// </summary>
/// <remarks>
///     Deliberately does NOT redeclare the timers the contract also names but that already live on sibling
///     partials and are already ticked by their own systems: <see cref="DropItemTime" /> (group-A lucky-drop,
///     PlayerRuntimeState.Consumables.cs), <see cref="TaiyanKeyTimer" /> (aZone125Time, same file),
///     <see cref="PetExpX2Time" /> (group-A pet double-exp, PlayerRuntimeState.Companions.cs, decremented by
///     <see cref="Simulation.PetExpBoostCountdownSystem" />), <see cref="BuffX2Time" /> (group-C double-buff,
///     PlayerRuntimeState.BuffDurationMultiplier.cs, decremented by
///     <see cref="Simulation.SupportSkillTimeUpRatioMaintenanceSystem" />), <see cref="PremiumExpireUtc" />
///     (same file, cleared by that same system), and <see cref="Level2" /> (the zone-101 gate, on the core
///     partial). The zones 234-240 (<c>__GOD__</c>) tail of the same legacy block is likewise already owned by
///     <see cref="Simulation.HoisundoCountdownSystem" /> (<see cref="HoisundoTimeRemaining" />), so it is not
///     duplicated here either.
///     <para>
///         Every counter below is session-scoped only (no persisted game.Characters column and no grant path
///         wired yet), the same "real API, currently starts at its C# default in production" posture
///         <see cref="HoisundoTimeRemaining" />'s own remarks document for its sibling not-yet-persisted zone
///         timer -- so in practice these all start at 0 on a fresh zone entry and the paid-zone counters
///         (<see cref="Zone101Time" />/<see cref="TaiyanKeyTimer" />/<see cref="Zone126Time" />/
///         <see cref="Zone050Time2" />) evict a non-privileged occupant at the first real-minute boundary until
///         a follow-up contract supplies a grant/hydration path. See
///         <see cref="Simulation.TimedBuffCountdownSystem" />'s own remarks for the full per-timer citation trail.
///     </para>
/// </remarks>
public partial class PlayerRuntimeState
{
    // --- Group A (non-war zones), Server/ts25zone/S07_MyGame04.cpp:913-970 --------------------------------

    /// <summary>
    ///     aFightingGodForDestroy -- a double-experience variant countdown (minutes). Broadcast sub-code
    ///     S020DOUBLE_EXP_TIME1_112. Group A: decays only on a non-war zone (see
    ///     <see cref="Simulation.TimedBuffCountdownSystem" />'s server-partition gate).
    /// </summary>
    public int FightingGodForDestroy { get; set; }

    /// <summary>aDoubleExpTime1 -- double-experience countdown (minutes). Broadcast sub-code S017DOUBLE_EXP_TIME. Group A.</summary>
    public int DoubleExpTime1 { get; set; }

    /// <summary>
    ///     aDoubleExpTime2 -- second double-experience countdown (minutes). Broadcast sub-code S043DOUBLE_EXP_TIME2.
    ///     Group A.
    /// </summary>
    public int DoubleExpTime2 { get; set; }

    // --- Group B (war/RvR zones), Server/ts25zone/S07_MyGame04.cpp:972-1025 --------------------------------

    /// <summary>
    ///     aAnimalDoubleExp -- mount double-experience countdown (minutes, <c>USE_ANIMAL</c>). Broadcast sub-code
    ///     S075DOUBLE_MOUNT_EXP. Group B, and additionally gated on a mount being active with its accumulated
    ///     experience still below MAX_MOUNT_EXP (100000) -- see
    ///     <see cref="Simulation.TimedBuffCountdownSystem" />. Currently never decrements in practice: no mount
    ///     acquisition path exists yet (<see cref="AnimalIndex" />/<see cref="AnimalNumber" /> stay at their
    ///     no-mount defaults), same "real but currently unreachable" posture as the rest of the mount family.
    /// </summary>
    public int AnimalDoubleExp { get; set; }

    /// <summary>
    ///     aDmgBoost -- Damage-Boost combat-scroll countdown (minutes). Broadcast sub-code S046DAMGE_BOOST_SCROLL. Group
    ///     B.
    /// </summary>
    public int DmgBoost { get; set; }

    /// <summary>aHPBoost -- HP-Boost combat-scroll countdown (minutes). Broadcast sub-code S047HEALT_BOOST_SCROLL. Group B.</summary>
    public int HPBoost { get; set; }

    /// <summary>
    ///     aCriBoost -- Critical-Boost combat-scroll countdown (minutes). Broadcast sub-code S048CRITICAL_BOOST_SCROLL.
    ///     Group B.
    /// </summary>
    public int CriBoost { get; set; }

    /// <summary>aWarriorPill -- Warrior Elixir countdown (minutes). Broadcast sub-code S091WARRIOR_ELIXIR. Group B.</summary>
    public int WarriorPill { get; set; }

    /// <summary>aWarriorScroll -- Warrior Scroll countdown (minutes). Broadcast sub-code S087WARRIOR_SCROLL. Group B.</summary>
    public int WarriorScroll { get; set; }

    // --- Paid-zone occupancy counters, Server/ts25zone/S07_MyGame04.cpp:1046-1133 --------------------------

    /// <summary>
    ///     aZone101Time -- zone-101 occupancy countdown (minutes). Broadcast sub-code S018ZONE_101_TIME. The
    ///     whole zone-101 countdown/broadcast/eviction only runs while <see cref="Level2" /> is greater than 0
    ///     (a below-threshold character is neither charged nor evicted in zone 101).
    /// </summary>
    public int Zone101Time { get; set; }

    /// <summary>
    ///     aZone126Time -- zone-126 occupancy countdown (minutes). Broadcast sub-code S022ZONE_126_TIME. Active
    ///     only while <see cref="PremiumExpireUtc" /> is not currently in the future -- an active premium
    ///     suspends both the countdown and the eviction (see <see cref="Simulation.TimedBuffCountdownSystem" />).
    /// </summary>
    public int Zone126Time { get; set; }

    /// <summary>
    ///     aZone050Time2 -- the server-52 occupancy countdown (minutes). Broadcast sub-code S065ITEM_1219. Same
    ///     eviction shape as the 101/125/126 zones, adjacent in the same legacy block
    ///     (Server/ts25zone/S07_MyGame04.cpp:1120-1133).
    /// </summary>
    public int Zone050Time2 { get; set; }

    // --- Gate / cadence / eviction plumbing ---------------------------------------------------------------

    /// <summary>
    ///     Legacy <c>uUserSort</c> -- the account privilege/class code. The entire paid-zone countdown-and-
    ///     eviction block applies only while this is below 1; any privileged user (1 or above) is wholly exempt
    ///     (Server/ts25zone/S07_MyGame04.cpp:1046). Session-scoped only and NOT hydrated yet: sourced in legacy
    ///     from the account grade carried through the session ticket
    ///     (<c>Fenrir.Network.Dispatch.Zone.Sessions.ZoneClientSession.AccountGrade</c>). Until a world-entry/
    ///     zone-transfer hydration is wired, this defaults to 0, so every occupant (including an un-hydrated GM)
    ///     is treated as an ordinary player subject to the paid-zone block -- see
    ///     <see cref="Simulation.TimedBuffCountdownSystem" />'s own remarks and the workstream's wiring note.
    /// </summary>
    public int UserSort { get; set; }

    /// <summary>
    ///     Legacy-tick accumulator for <see cref="Simulation.TimedBuffCountdownSystem" />'s own once-per-real-
    ///     minute cadence -- deliberately a separate counter from <see cref="PlayTimeAccrualTicks" />/
    ///     <see cref="SupportSkillTimeUpRatioAccrualTicks" />/<see cref="PetExpX2TimeAccrualTicks" />/
    ///     <see cref="HoisundoAccrualTicks" /> even though all share the same 120-legacy-tick (60s) period, for
    ///     the same reason those siblings each keep their own: sharing one accumulator across independently-owned
    ///     per-minute systems would double-consume/desync them.
    /// </summary>
    public int TimedBuffCountdownAccrualTicks { get; set; }

    /// <summary>
    ///     Latched true by <see cref="Simulation.TimedBuffCountdownSystem" /> when a non-privileged occupant's
    ///     applicable paid-zone counter is found already at 0 on a per-minute boundary (the eviction case,
    ///     Server/ts25zone/S07_MyGame04.cpp:1060-1061,1076-1077,1114-1115). The system deliberately does NOT
    ///     tear the session down itself; this flag is the hand-off the zone's own session-teardown stage reads
    ///     to <c>Abort</c> the occupant (<see cref="Simulation.DeathGateTickSystem" />/
    ///     <see cref="Simulation.HoisundoCountdownSystem" /> show the deferred-abort shape). Never cleared here
    ///     -- a latch the consumer acts on once.
    /// </summary>
    public bool PaidZoneEvictionPending { get; set; }
}
