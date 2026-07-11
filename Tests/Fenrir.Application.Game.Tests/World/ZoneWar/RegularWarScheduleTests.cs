using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="RegularWarSchedule" />: the Idle/OpenGate/PreWar/Active/PostWarCleanup/ForcedReset
///     phase machine, capture/score, and victory determination. Every tick count below is derived directly
///     from the class's own public tick constants so a future constant change fails these tests loudly instead
///     of silently drifting.
/// </summary>
public class RegularWarScheduleTests
{
    private static readonly RegularWarMapConfig OrdinaryMap = Get(49);
    private static readonly RegularWarMapConfig SmallestTribeMap = Get(160);
    private static readonly RegularWarMapConfig BossWarMap = Get(295);

    private static RegularWarMapConfig Get(short mapId)
    {
        Assert.True(RegularWarMapCatalog.TryGet(mapId, out var config));
        return config;
    }

    private static RegularWarEnvironmentSnapshot Present(int total, int t0 = 0, int t1 = 0, int t2 = 0, int t3 = 0)
    {
        return new RegularWarEnvironmentSnapshot(total, [t0, t1, t2, t3]);
    }

    private static RegularWarTickResult TickMany(RegularWarSchedule schedule, int count,
        RegularWarEnvironmentSnapshot snapshot)
    {
        var result = default(RegularWarTickResult);
        for (var i = 0; i < count; i++)
            result = schedule.Tick(snapshot);
        return result;
    }

    /// <summary>Ticks a fresh schedule all the way to the exact tick the active-war window begins.</summary>
    private static RegularWarSchedule AdvanceToActive(RegularWarMapConfig config,
        RegularWarEnvironmentSnapshot? preWarSnapshot = null)
    {
        var schedule = new RegularWarSchedule(config);
        var snapshot = preWarSnapshot ?? Present(4, 1, 1, 1, 1);

        var totalTicks = RegularWarSchedule.CooldownTicks +
                         RegularWarSchedule.CountdownAnnounceStartValue *
                         RegularWarSchedule.CountdownAnnounceIntervalTicks +
                         RegularWarSchedule.FinalWaitTicks +
                         RegularWarSchedule.OpenGateTicks +
                         RegularWarSchedule.PreWarTicks;

        RegularWarTickResult last = default;
        for (var i = 0; i < totalTicks; i++)
            last = schedule.Tick(snapshot);

        Assert.True(last.EnteredActiveWar);
        Assert.Equal(RegularWarPhase.Active, schedule.Phase);
        Assert.Equal(RegularWarSchedule.ActiveWarDurationTicks, schedule.RemainingActiveWarTicks);
        return schedule;
    }

    [Fact]
    public void FreshSchedule_StartsIdle_AtWarCycleOne()
    {
        var schedule = new RegularWarSchedule(OrdinaryMap);
        Assert.Equal(RegularWarPhase.Idle, schedule.Phase);
        Assert.Equal(1, schedule.WarCycleNumber);
    }

    [Fact]
    public void Cooldown_AlwaysWaitsTheFullInterval_EvenOnTheVeryFirstCycle()
    {
        var schedule = new RegularWarSchedule(OrdinaryMap);
        var snapshot = Present(0);

        TickMany(schedule, RegularWarSchedule.CooldownTicks - 1, snapshot);
        Assert.Equal(RegularWarPhase.Idle, schedule.Phase);
        Assert.Equal(1, schedule.WarCycleNumber); // not yet incremented

        schedule.Tick(snapshot); // the exact CooldownTicks-th tick
        Assert.Equal(2, schedule.WarCycleNumber); // cooldown elapsed -> new cycle
    }

    [Fact]
    public void CountdownAnnounce_CountsDownFromTenToOne_ThenAdvancesToFinalWait()
    {
        var schedule = new RegularWarSchedule(OrdinaryMap);
        var snapshot = Present(0);

        TickMany(schedule, RegularWarSchedule.CooldownTicks, snapshot); // enter CountdownAnnounce sub-phase

        var announced = new List<int>();
        for (var i = 0; i < RegularWarSchedule.CountdownAnnounceStartValue; i++)
        {
            var result = TickMany(schedule, RegularWarSchedule.CountdownAnnounceIntervalTicks, snapshot);
            Assert.NotNull(result.CountdownAnnounceValue);
            announced.Add(result.CountdownAnnounceValue!.Value);
        }

        Assert.Equal([10, 9, 8, 7, 6, 5, 4, 3, 2, 1], announced);

        // Still Idle -- FinalWait hasn't elapsed yet.
        Assert.Equal(RegularWarPhase.Idle, schedule.Phase);

        var afterFinalWait = TickMany(schedule, RegularWarSchedule.FinalWaitTicks, snapshot);
        Assert.Equal(RegularWarPhase.OpenGate, afterFinalWait.Phase);
    }

    [Fact]
    public void OpenGate_WaitsThreeMinutes_ThenAdvancesToPreWar()
    {
        var schedule = new RegularWarSchedule(OrdinaryMap);
        var snapshot = Present(0);
        var idleTotal = RegularWarSchedule.CooldownTicks +
                        RegularWarSchedule.CountdownAnnounceStartValue *
                        RegularWarSchedule.CountdownAnnounceIntervalTicks +
                        RegularWarSchedule.FinalWaitTicks;

        TickMany(schedule, idleTotal, snapshot);
        Assert.Equal(RegularWarPhase.OpenGate, schedule.Phase);

        TickMany(schedule, RegularWarSchedule.OpenGateTicks - 1, snapshot);
        Assert.Equal(RegularWarPhase.OpenGate, schedule.Phase);

        var result = schedule.Tick(snapshot);
        Assert.Equal(RegularWarPhase.PreWar, result.Phase);
    }

    [Fact]
    public void PreWar_OnServer160Only_FlagsTheSmallestPresentTribe()
    {
        var schedule = new RegularWarSchedule(SmallestTribeMap);
        var snapshot = Present(6, 3, 1, 2);
        var preWarTotal = RegularWarSchedule.CooldownTicks +
                          RegularWarSchedule.CountdownAnnounceStartValue *
                          RegularWarSchedule.CountdownAnnounceIntervalTicks +
                          RegularWarSchedule.FinalWaitTicks +
                          RegularWarSchedule.OpenGateTicks +
                          RegularWarSchedule.PreWarTicks;

        var last = TickMany(schedule, preWarTotal, snapshot);

        Assert.Equal((byte)3, last.SmallestPresentTribe); // tribe 3 has 0 present, the fewest
        Assert.Equal(RegularWarPhase.Active, schedule.Phase);
    }

    [Fact]
    public void PreWar_OnAnyOtherMap_NeverFlagsASmallestTribe()
    {
        var fresh = new RegularWarSchedule(OrdinaryMap);
        var snapshot = Present(6, 3, 1, 2);
        var preWarTotal = RegularWarSchedule.CooldownTicks +
                          RegularWarSchedule.CountdownAnnounceStartValue *
                          RegularWarSchedule.CountdownAnnounceIntervalTicks +
                          RegularWarSchedule.FinalWaitTicks +
                          RegularWarSchedule.OpenGateTicks +
                          RegularWarSchedule.PreWarTicks;

        var last = TickMany(fresh, preWarTotal, snapshot);

        Assert.Null(last.SmallestPresentTribe);
    }

    [Fact]
    public void Active_EmptyMap_AbortsImmediately_NoReward_SkipsStraightToForcedReset()
    {
        var schedule = AdvanceToActive(OrdinaryMap);

        var result = schedule.Tick(Present(0));

        Assert.Equal(RegularWarOutcome.AbortedEmptyMap, result.Outcome);
        Assert.Null(result.WinningTribe);
        Assert.True(result.MonstersShouldDespawn);
        Assert.Equal(RegularWarPhase.ForcedReset, result.Phase);
    }

    [Fact]
    public void Active_TwoOrMoreTribesPresent_ContinuesWithNoOutcome_AndCountdownDecrementsEveryTick()
    {
        var schedule = AdvanceToActive(OrdinaryMap);

        var result = TickMany(schedule, RegularWarSchedule.ActiveEvaluationCadenceTicks, Present(4, 2, 2));

        Assert.Null(result.Outcome);
        Assert.Equal(RegularWarPhase.Active, schedule.Phase);
        Assert.Equal(RegularWarSchedule.ActiveWarDurationTicks - RegularWarSchedule.ActiveEvaluationCadenceTicks,
            schedule.RemainingActiveWarTicks);
    }

    [Fact]
    public void Active_ElimintationVictory_CanHappenLongBeforeTheCountdownExpires()
    {
        var schedule = AdvanceToActive(OrdinaryMap);

        // Only tribe 2 is present -- immediate elimination win, independent of the kill tally.
        var result = TickMany(schedule, RegularWarSchedule.ActiveEvaluationCadenceTicks, Present(3, 0, 0, 3));

        Assert.Equal(RegularWarOutcome.TribeWin, result.Outcome);
        Assert.Equal((byte)2, result.WinningTribe);
        Assert.True(schedule.RemainingActiveWarTicks > 0); // won well before timeout
    }

    [Fact]
    public void Active_AllFourTribesAbsentButMapNotEmpty_IsADraw_DistinctFromAbortedEmptyMap()
    {
        var schedule = AdvanceToActive(OrdinaryMap);

        // 2 untribed characters remain -- map is not empty, but no tribe has any presence.
        var result = TickMany(schedule, RegularWarSchedule.ActiveEvaluationCadenceTicks, Present(2));

        Assert.Equal(RegularWarOutcome.Draw, result.Outcome);
        Assert.Null(result.WinningTribe);
    }

    [Fact]
    public void Active_KillTally_OnlyCountsWhileActiveAndCountdownPositive()
    {
        var schedule = new RegularWarSchedule(OrdinaryMap);

        // Outside Active entirely -- never counts.
        schedule.RegisterKill(1, 100);
        Assert.Equal(0, schedule.GetTribeKillTally(1));

        AdvanceInPlace(schedule);

        schedule.RegisterKill(1, 100);
        schedule.RegisterKill(1, 100);
        schedule.RegisterKill(2, 200);

        Assert.Equal(2, schedule.GetTribeKillTally(1));
        Assert.Equal(1, schedule.GetTribeKillTally(2));
        // .ToArray() -- ImmutableArray<T>'s own IEquatable<T> is backing-array reference equality, not
        // elementwise, so comparing it directly against a collection-expression literal would take that
        // reference-equality path (via T,T type inference) and spuriously fail despite identical contents.
        Assert.Equal([100, 200], schedule.GetTopKillers().ToArray());

        void AdvanceInPlace(RegularWarSchedule s)
        {
            var snapshot = Present(4, 1, 1, 1, 1);
            var total = RegularWarSchedule.CooldownTicks +
                        RegularWarSchedule.CountdownAnnounceStartValue *
                        RegularWarSchedule.CountdownAnnounceIntervalTicks +
                        RegularWarSchedule.FinalWaitTicks +
                        RegularWarSchedule.OpenGateTicks +
                        RegularWarSchedule.PreWarTicks;
            for (var i = 0; i < total; i++)
                s.Tick(snapshot);
        }
    }

    [Fact]
    public void Active_Timeout_StrictlyHighestTribeTally_Wins()
    {
        var schedule = AdvanceToActive(OrdinaryMap);

        for (var i = 0; i < 5; i++)
            schedule.RegisterKill(0, 10 + i);
        schedule.RegisterKill(1, 999);

        // 2+ tribes present throughout so the elimination path never preempts the timeout comparison.
        var result = TickMany(schedule, RegularWarSchedule.ActiveWarDurationTicks, Present(4, 2, 2));

        Assert.Equal(RegularWarOutcome.TribeWin, result.Outcome);
        Assert.Equal((byte)0, result.WinningTribe);
        Assert.Equal(0, schedule.RemainingActiveWarTicks);
    }

    [Fact]
    public void Active_Timeout_TiedTallies_IsADraw()
    {
        var schedule = AdvanceToActive(OrdinaryMap);

        schedule.RegisterKill(0, 1);
        schedule.RegisterKill(1, 2);

        var result = TickMany(schedule, RegularWarSchedule.ActiveWarDurationTicks, Present(4, 2, 2));

        Assert.Equal(RegularWarOutcome.Draw, result.Outcome);
        Assert.Null(result.WinningTribe);
    }

    [Fact]
    public void Active_Timeout_AllTalliesZero_IsADraw()
    {
        var schedule = AdvanceToActive(OrdinaryMap);

        var result = TickMany(schedule, RegularWarSchedule.ActiveWarDurationTicks, Present(4, 2, 2));

        Assert.Equal(RegularWarOutcome.Draw, result.Outcome);
        Assert.Null(result.WinningTribe);
    }

    [Fact]
    public void WarConclusion_SetsMonstersShouldDespawn_AndBossSpawnOnlyOnBossMapWin()
    {
        var ordinary = AdvanceToActive(OrdinaryMap);
        var ordinaryResult = TickMany(ordinary, RegularWarSchedule.ActiveEvaluationCadenceTicks,
            Present(3, 0, 0, 3));
        Assert.True(ordinaryResult.MonstersShouldDespawn);
        Assert.False(ordinaryResult.BossMonstersShouldSpawn); // not the boss-war map

        var boss = AdvanceToActive(BossWarMap);
        var bossWinResult = TickMany(boss, RegularWarSchedule.ActiveEvaluationCadenceTicks, Present(3, 0, 0, 3));
        Assert.Equal(RegularWarOutcome.TribeWin, bossWinResult.Outcome);
        Assert.True(bossWinResult.BossMonstersShouldSpawn);

        var bossDraw = AdvanceToActive(BossWarMap);
        var bossDrawResult = TickMany(bossDraw, RegularWarSchedule.ActiveEvaluationCadenceTicks, Present(2));
        Assert.Equal(RegularWarOutcome.Draw, bossDrawResult.Outcome);
        Assert.False(bossDrawResult.BossMonstersShouldSpawn); // draw never spawns the boss
    }

    [Fact]
    public void PostWarCleanup_OrdinaryMap_WaitsThenForcedReset_ThenDisconnectsAfterTwelveTicks_ThenBackToIdle()
    {
        var schedule = AdvanceToActive(OrdinaryMap);
        TickMany(schedule, RegularWarSchedule.ActiveEvaluationCadenceTicks, Present(3, 0, 0, 3)); // conclude
        Assert.Equal(RegularWarPhase.PostWarCleanup, schedule.Phase);

        var empty = Present(0);
        TickMany(schedule, RegularWarSchedule.PostWarCleanupTicks - 1, empty);
        Assert.Equal(RegularWarPhase.PostWarCleanup, schedule.Phase);

        var afterCleanup = schedule.Tick(empty);
        Assert.Equal(RegularWarPhase.ForcedReset, afterCleanup.Phase);
        Assert.True(afterCleanup.MonstersShouldDespawn);

        TickMany(schedule, RegularWarSchedule.ForcedResetDisconnectAtTicks - 1, empty);
        Assert.Equal(RegularWarPhase.ForcedReset, schedule.Phase);

        var afterDisconnect = schedule.Tick(empty);
        Assert.True(afterDisconnect.AllSessionsShouldDisconnect);
        Assert.Equal(RegularWarPhase.Idle, afterDisconnect.Phase);
        Assert.Equal(0, schedule.RemainingActiveWarTicks);
    }

    [Fact]
    public void PostWarCleanup_BossMap_BossAlreadyGone_TimesOutAtFlatPollWindow()
    {
        var schedule = AdvanceToActive(BossWarMap);
        TickMany(schedule, RegularWarSchedule.ActiveEvaluationCadenceTicks, Present(3, 0, 0, 3)); // TribeWin
        Assert.Equal(RegularWarPhase.PostWarCleanup, schedule.Phase);

        var bossGone = new RegularWarEnvironmentSnapshot(1, [0, 0, 1, 0]);

        // Confirmed legacy defect (A4-missing-bosses): the post-war "is the boss still alive" comparison is
        // unconditionally true every tick, so BossMonsterAlive plays no role here -- this phase is governed
        // purely by the flat PostWarCleanupTicks + BossPollMaxTicks - 1 timeout (the -1 because the tick that
        // crosses the PostWarCleanupTicks threshold is the SAME Tick() call that starts the boss-poll count).
        TickMany(schedule,
            RegularWarSchedule.PostWarCleanupTicks + RegularWarSchedule.BossPollMaxTicks - 2, bossGone);
        Assert.Equal(RegularWarPhase.PostWarCleanup, schedule.Phase);

        var final = schedule.Tick(bossGone);
        Assert.Equal(RegularWarPhase.ForcedReset, final.Phase);
        Assert.True(final.MonstersShouldDespawn);
    }

    [Fact]
    public void PostWarCleanup_BossMap_BossReportedAliveWholeWindow_StillTimesOutAtSameFlatPollWindow()
    {
        var schedule = AdvanceToActive(BossWarMap);
        TickMany(schedule, RegularWarSchedule.ActiveEvaluationCadenceTicks, Present(3, 0, 0, 3)); // TribeWin

        var bossAlive = new RegularWarEnvironmentSnapshot(1, [0, 0, 1, 0], true);

        // Same flat PostWarCleanupTicks + BossPollMaxTicks - 1 timeout as the boss-already-gone sibling test
        // above -- BossMonsterAlive is carried on the snapshot but never actually read by TickPostWarCleanup
        // under the confirmed legacy defect, so reporting the boss alive for the WHOLE window changes nothing.
        TickMany(schedule,
            RegularWarSchedule.PostWarCleanupTicks + RegularWarSchedule.BossPollMaxTicks - 2, bossAlive);
        Assert.Equal(RegularWarPhase.PostWarCleanup, schedule.Phase);

        var final = schedule.Tick(bossAlive);
        Assert.Equal(RegularWarPhase.ForcedReset, final.Phase);
        Assert.True(final.MonstersShouldDespawn);
    }

    [Fact]
    public void LeaderboardAndKillTally_AreClearedOnlyAtTheNextCooldownElapse()
    {
        var schedule = AdvanceToActive(OrdinaryMap);
        schedule.RegisterKill(0, 42);

        TickMany(schedule, RegularWarSchedule.ActiveEvaluationCadenceTicks, Present(3, 3)); // conclude
        TickMany(schedule, RegularWarSchedule.PostWarCleanupTicks, Present(0)); // -> ForcedReset
        TickMany(schedule, RegularWarSchedule.ForcedResetDisconnectAtTicks, Present(0)); // -> Idle

        // Still populated immediately after returning to Idle -- only cleared on the next cooldown-elapse.
        // .ToArray() -- see the ImmutableArray<T> reference-equality note in Active_KillTally_... above.
        Assert.Equal([42], schedule.GetTopKillers().ToArray());

        TickMany(schedule, RegularWarSchedule.CooldownTicks, Present(0));
        Assert.Empty(schedule.GetTopKillers());
        Assert.Equal(3, schedule.WarCycleNumber);
    }

    [Fact]
    public void SmallestPresentTribe_TiesBrokenByLowestTribeId()
    {
        var fresh = new RegularWarSchedule(SmallestTribeMap);
        var snapshot = Present(4, 1, 1, 5, 5); // tribes 0 and 1 tie at the smallest count (1)
        var preWarTotal = RegularWarSchedule.CooldownTicks +
                          RegularWarSchedule.CountdownAnnounceStartValue *
                          RegularWarSchedule.CountdownAnnounceIntervalTicks +
                          RegularWarSchedule.FinalWaitTicks +
                          RegularWarSchedule.OpenGateTicks +
                          RegularWarSchedule.PreWarTicks;

        RegularWarTickResult last = default;
        for (var i = 0; i < preWarTotal; i++)
            last = fresh.Tick(snapshot);

        Assert.Equal((byte)0, last.SmallestPresentTribe);
    }
}
