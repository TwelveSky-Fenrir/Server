using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Boss-756 (Zone200 gate-breach/kill-race) summon and mechanic data. Legacy servers 200, 297, 298, and 299
///     run a gate-breach state machine that transitions into a per-tribe kill-race once the gate breaks, and
///     summons boss 756 once any one tribe's kill quota reaches zero. This class remains data only (mirroring
///     how <see cref="RegularWarMapCatalog" /> held Regular War's own per-map data before
///     <see cref="RegularWarSchedule" /> existed to consume it) -- but, unlike the stale wording an earlier
///     revision of this summary carried, the gate-breach/kill-race lifecycle itself IS now modeled, by
///     <see cref="ValleyWarSchedule" />/<see cref="ValleyWarSystem" />, which consume this catalog's data the
///     same way <see cref="RegularWarSchedule" /> consumes <see cref="RegularWarBossSummonCatalog" />'s for the
///     sibling Boss-561/Regular-War map family. See this class's own remarks for exactly which pieces of the
///     mechanic those two classes cover versus what remains genuinely unwired.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:1116-1137 (4-server Zone200 scope) ;
///         :11149-11572 (the kill-race phase's enclosing function) ; :11288-11304 (gate-breach -&gt; kill-race
///         transition, 170-per-tribe quota seed + 3600-tick clock, carried forward not independently reopened
///         this session) ; :11375-11429 (win-check tribe-order test and the boss-756 summon call) ;
///         :11451-11525 (the state-6 "battle win bonus" reward-unreachable comparison-bound defect, identical
///         shape to Boss 561's -- see that class's own remarks) ; Server/ts25zone/S07_MyGame02.cpp:3155-3177
///         (kill-quota decrement + the first of two independent XP/CP suppression guards) ;
///         Server/ts25zone/S07_MyGame05.cpp:3740-3759 (the second, redundant XP/CP suppression guard).
///     </para>
///     <para>
///         <b>The kill-drop side effect (item 1073, then 1447, then 723 on any death of a monster carrying id
///         756, regardless of spawn source) is ALREADY FULLY IMPLEMENTED</b> -- confirmed this session, NOT
///         duplicated here. See <see cref="Fenrir.Application.Game.Domain.World.Loot.BossDropCatalog.ThreeItemEventList" />
///         and <see cref="Fenrir.Application.Game.Domain.World.Loot.BossEventDropResolver.ThreeItemEventBossMonsterId" />
///         (C4 boss-drop workstream), which independently cite the identical
///         <c>Server/ts25zone/S07_MyGame05.cpp:2423-2428</c> range this contract also cites for the same three
///         items in the same order.
///     </para>
///     <para>
///         <b>No longer accurate as originally written -- corrected in place rather than left to contradict
///         itself:</b> an earlier revision of this remark flagged the gate-breach -&gt; kill-race phase
///         transitions, the per-tribe kill-quota decrement hook, AND the boss-756 summon primitive as all
///         "genuinely missing." That is no longer true for the first two:
///         <see cref="ValleyWarSchedule" />/<see cref="ValleyWarSystem" /> now model the phase transitions
///         themselves, and the kill-quota decrement hook is wired
///         (<see cref="Monsters.MonsterSpawnScheduler" />'s per-kill credit path calls
///         <see cref="ValleyWarKillRegistry.RegisterMonsterKill" />, reaching the SAME schedule instance
///         <see cref="ValleyWarSystem.Simulate" /> ticks for that map). The boss-756 summon PRIMITIVE this
///         catalog's <see cref="SummonX" />/<see cref="SummonY" />/<see cref="SummonZ" />/
///         <see cref="ExistenceCheckActive" /> feed drives is ALSO now wired end-to-end --
///         <see cref="ValleyWarSystem" />'s <c>React</c> method calls <see cref="Zone.HandleSummonValleyWarBoss" />
///         directly on a tribe kill-race win, this catalog's data no longer awaiting a "future" state machine to
///         consume it. <b>Still genuinely missing:</b> only the twin XP/CP-suppression guards on every
///         Zone200-flagged kill (Server/ts25zone/S07_MyGame02.cpp:3155-3177 and
///         Server/ts25zone/S07_MyGame05.cpp:3740-3759, cited above) -- that narrow gap needs its own hook point
///         inside the existing combat-resolution/kill-credit pipeline and is unrelated to the boss summon
///         primitive this revision closes.
///     </para>
///     <para>
///         <b>The "battle win bonus" 8-item reward list is data-only, dead-in-practice, and deliberately
///         incomplete.</b> Per the contract's own confirmed defect (identical comparison-bound bug to Boss
///         561's post-war poll, re-verified independently this session): the tribe-wide reward block is
///         provably unreachable in the shipped build, so this table exists purely for citation completeness --
///         it must never be wired to an actual grant path. One of its 8 entries (a server-computed random
///         "animal" item, not itself resolved to a concrete id by the source contract) is deliberately omitted
///         from <see cref="BattleWinBonusFixedItemIds" /> rather than guessed at -- see that member's own
///         remarks. Never invent an id for it.
///     </para>
/// </remarks>
public static class Zone200GateBreachBossCatalog
{
    /// <summary>The four legacy server numbers the entire Zone200 gate-breach/kill-race mechanic applies to.</summary>
    public static readonly ImmutableArray<short> EligibleServerNumbers = [200, 297, 298, 299];

    /// <summary>The monster catalog id summoned once any one tribe's kill quota reaches exactly zero.</summary>
    public const int BossMonsterId = 756;

    /// <summary>Per-tribe kill quota, identical for all four tribes, seeded when the kill-race phase begins.</summary>
    public const int KillQuotaPerTribe = 170;

    /// <summary>
    ///     ~30 real minutes at the engine's ~500 ms legacy tick (Server/Header/function.h:1643-1646's own
    ///     tick-to-real-time basis) -- the clock seeded alongside <see cref="KillQuotaPerTribe" /> when the
    ///     gate-breach phase transitions into the kill-race phase. Carried forward from the input finding, not
    ///     independently reopened this session.
    /// </summary>
    public const int GateBreachToKillRaceClockTicks = 3600;

    /// <summary>Fixed boss-756 summon position, first axis.</summary>
    public const float SummonX = 16f;

    /// <summary>Fixed boss-756 summon position, second axis.</summary>
    public const float SummonY = 264f;

    /// <summary>Fixed boss-756 summon position, third axis.</summary>
    public const float SummonZ = 7650f;

    /// <summary>
    ///     This summon call configures its existence-check scan ON -- contrast
    ///     <see cref="RegularWarBossSummonCatalog.SkipExistenceCheck" />, Boss 561's summon, which disables it.
    ///     If a copy of monster 756 already exists in the special/boss monster slot range, this summon call is
    ///     a silent no-op.
    /// </summary>
    public const bool ExistenceCheckActive = true;

    /// <summary>
    ///     ~30 real minutes -- the post-summon "wait for boss 756 to die or the timeout to expire" phase's own
    ///     fixed timeout, identical shape (and identical comparison-bound defect) to Boss 561's
    ///     <see cref="RegularWarSchedule.BossPollMaxTicks" />. Per the confirmed defect, the tribe-wide reward
    ///     block immediately after this phase (<see cref="BattleWinBonusFixedItemIds" />) is provably
    ///     unreachable in the shipped build -- this phase always resolves via this flat timeout, never via a
    ///     genuine liveness detection.
    /// </summary>
    public const int PostSummonBossDeathPollTimeoutTicks = 3600;

    /// <summary>
    ///     7 of the 8 items the (dead-in-practice, see class remarks) tribe-wide "battle win bonus" would grant,
    ///     in the source's own order, with the 8th slot (a server-computed random "animal" item between indices
    ///     4 and 5, i.e. between item 1145 and item 2249) deliberately omitted -- the source contract this class
    ///     was implemented from did not resolve that slot to a concrete item id, and per this project's
    ///     "never invent a magnitude/id" rule it is not guessed at here. A future contract citing that concrete
    ///     id can complete this list; do not populate the gap without one. This data is never consumed by any
    ///     live Fenrir path (see class remarks) -- kept purely for citation completeness.
    /// </summary>
    public static readonly ImmutableArray<int> BattleWinBonusFixedItemIds = [1072, 1103, 1449, 1422, 1145, 2249, 602];
}
