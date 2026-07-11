namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Boss-561 summon data for the Regular War (Zone049) boss-war map (legacy server 295 only --
///     <see cref="RegularWarMapConfig.IsBossWar" />). Supplies the concrete monster id / summon count / fixed
///     position / existence-check posture that <see cref="RegularWarSchedule" />'s own
///     <see cref="RegularWarTickResult.BossMonstersShouldSpawn" /> flag reports as due but does not itself
///     carry -- <see cref="RegularWarSchedule" /> is deliberately data-free (see its own class remarks,
///     "reward VALUE computation ... and every actual mutation ... this class only ever reports that they are
///     due").
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:4940-4979 (the summon-count/position lookup table --
///         only server 295's row is ever observably consumed, since the boss-war flag that gates reaching the
///         summon step is true only for 295, per Server/ts25zone/S07_MyGame01.cpp:687-690) ;
///         Server/ts25zone/S07_MyGame01.cpp:5410-5432 (the summon-on-win block itself: existing summoned
///         monster(s) cleared, then exactly 3 summon calls back to back) ;
///         Server/ts25zone/S10_MySummon.cpp:1254-1297 (the shared summon routine's existence-check semantics --
///         this particular call configures it OFF, so each of the 3 calls succeeds unconditionally as long as a
///         free slot exists in the special/boss monster range).
///     </para>
///     <para>
///         <b>Wiring status: wired (A4-missing-bosses).</b> <see cref="RegularWarSchedule" /> computes and
///         reports <see cref="RegularWarTickResult.BossMonstersShouldSpawn" /> correctly (server-295-only,
///         only on a decisive <see cref="RegularWarOutcome.TribeWin" />), and
///         <see cref="Fenrir.Application.Game.Hosting.World.ZoneWar.RegularWarSchedulerHost" /> threads that
///         flag through to <see cref="Fenrir.Application.Game.Hosting.World.ZoneWar.IRegularWarEventSink.OnWarConcluded" />.
///         <c>Fenrir.Application.Game.Hosting.World.ZoneWar.RegularWarRewardGrantSink</c> now consumes this
///         catalog's constants and posts <c>ZoneCommand.SummonRegularWarBoss</c> onto the concluding map's own
///         <see cref="Zone" /> (the single-writer-per-zone posting precedent every other cross-thread
///         <see cref="Zone" /> mutation in this codebase already follows), handled by
///         <see cref="Zone.HandleSummonRegularWarBoss" /> (<c>Zone.RegularWarBossSummon.cs</c>) -- see that
///         method's own remarks for the summon primitive itself and for the one deliberately-NOT-modeled piece
///         (the preceding legacy map-wide "clear every summoned monster" step).
///     </para>
///     <para>
///         <b>Also not yet wired: the post-war "is the boss still alive" poll's confirmed comparison-bound
///         defect.</b> <see cref="RegularWarSchedule.TickPostWarCleanup" /> currently treats
///         <see cref="RegularWarEnvironmentSnapshot.BossMonsterAlive" /> as a genuine liveness signal feeding a
///         two-stage poll (<see cref="RegularWarSchedule.BossPollMaxTicks" /> then
///         <see cref="RegularWarSchedule.BossConfirmedAbsenceTicks" />) -- but the legacy source this was built
///         from has a confirmed defect (re-verified independently by the A4-missing-bosses contract this class
///         was implemented from) where the liveness scan's result is compared against the wrong bound, making
///         the "boss still alive, keep waiting" branch unconditionally true every tick regardless of the
///         scanned value. Net legacy effect: the wait is governed purely by the flat
///         <see cref="RegularWarSchedule.BossPollMaxTicks" /> timeout (~15 real minutes); the
///         <see cref="RegularWarSchedule.BossConfirmedAbsenceTicks" /> sub-wait is legacy-unreachable. Today's
///         Fenrir <see cref="RegularWarSchedulerHost" /> always reports <c>BossMonsterAlive = false</c>
///         (documented gap in that class's own remarks: "no monster catalog entry identified yet"), which
///         produces the OPPOSITE of the legacy defect -- an immediate false "confirmed absence" and only a
///         ~90 s wait, not the ~15 min the defect actually produces. This is a real behavioral mismatch this
///         data-only slice cannot safely fix in place (it requires editing <see cref="RegularWarSchedule" />'s
///         tested state-machine body plus two existing tests that assert the current, now-known-incorrect,
///         genuine-poll behavior) -- flagged precisely in this cluster's wiring report for a coordinated
///         production+test change, not attempted here.
///     </para>
/// </remarks>
public static class RegularWarBossSummonCatalog
{
    /// <summary>The monster catalog id summoned three times on a decisive server-295 Regular War conclusion.</summary>
    public const int BossMonsterId = 561;

    /// <summary>Exactly 3 summon calls, back to back, each independently subject to its own free-slot check.</summary>
    public const int SummonCount = 3;

    /// <summary>Fixed summon position, first axis (Server/ts25zone/S07_MyGame01.cpp:4965-4967).</summary>
    public const float SummonX = 58f;

    /// <summary>Fixed summon position, second axis.</summary>
    public const float SummonY = 100f;

    /// <summary>Fixed summon position, third axis.</summary>
    public const float SummonZ = 4190f;

    /// <summary>
    ///     This summon call configures its existence-check scan OFF -- contrast
    ///     <see cref="Zone200GateBreachBossCatalog.ExistenceCheckActive" />, Boss 756's summon, which enables
    ///     it. All 3 calls therefore succeed unconditionally as long as a free slot exists in the special/boss
    ///     monster slot range, regardless of whether a copy of 561 is already present.
    /// </summary>
    public const bool SkipExistenceCheck = true;
}
