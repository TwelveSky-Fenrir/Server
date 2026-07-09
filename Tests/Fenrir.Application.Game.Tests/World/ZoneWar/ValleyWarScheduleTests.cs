using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="ValleyWarSchedule" />: the Idle/GateCountdown/GateOpen/DoorPending/KillRace/
///     ScrollPending/BossWindow/PostWinCooldown/PreReset phase machine for Valley of the Deceased (Zone
///     200/297/298/299) -- a DISTINCT state machine from <see cref="RegularWarSchedule" />'s own Zone049
///     family (see <see cref="RegularWarScheduleTests" />, which this task leaves entirely unchanged). Every
///     tick count below is derived directly from the class's own public tick constants so a future constant
///     change fails these tests loudly instead of silently drifting.
/// </summary>
public class ValleyWarScheduleTests
{
    private static ValleyWarEnvironmentSnapshot Present(bool eligible = true, bool bossSlotOccupied = false)
    {
        return new ValleyWarEnvironmentSnapshot(eligible, bossSlotOccupied);
    }

    private static ValleyWarTickResult TickMany(ValleyWarSchedule schedule, int count,
        ValleyWarEnvironmentSnapshot? snapshot = null)
    {
        var s = snapshot ?? ValleyWarEnvironmentSnapshot.Empty;
        var result = default(ValleyWarTickResult);
        for (var i = 0; i < count; i++)
            result = schedule.Tick(s);
        return result;
    }

    private static ValleyWarSchedule AdvanceToGateCountdownStart()
    {
        var schedule = new ValleyWarSchedule();
        TickMany(schedule, ValleyWarSchedule.IdleWaitTicks);
        Assert.Equal(ValleyWarPhase.GateCountdown, schedule.Phase);
        return schedule;
    }

    private static ValleyWarSchedule AdvanceToGateOpenStart()
    {
        var schedule = AdvanceToGateCountdownStart();
        // 5 countdown announcements (5,4,3,2,1) then 1 more interval to actually open the gate.
        TickMany(schedule,
            (ValleyWarSchedule.GateCountdownStartValue + 1) * ValleyWarSchedule.GateCountdownIntervalTicks);
        Assert.Equal(ValleyWarPhase.GateOpen, schedule.Phase);
        return schedule;
    }

    private static ValleyWarSchedule AdvanceToDoorPendingStart()
    {
        var schedule = AdvanceToGateOpenStart();
        TickMany(schedule, ValleyWarSchedule.GateOpenTicks);
        Assert.Equal(ValleyWarPhase.DoorPending, schedule.Phase);
        return schedule;
    }

    private static ValleyWarSchedule AdvanceToKillRaceStart()
    {
        var schedule = AdvanceToDoorPendingStart();
        TickMany(schedule, ValleyWarSchedule.DoorPendingTicks);
        Assert.Equal(ValleyWarPhase.KillRace, schedule.Phase);
        return schedule;
    }

    /// <summary>Tribe 0 wins the kill race (via <see cref="ValleyWarSchedule.ForceZeroTribeQuota" />), then
    /// waits out the scroll-delete delay, landing exactly at the first tick of <see cref="ValleyWarPhase.BossWindow" />.</summary>
    private static ValleyWarSchedule AdvanceToBossWindowStart()
    {
        var schedule = AdvanceToKillRaceStart();
        schedule.ForceZeroTribeQuota(0);
        var win = schedule.Tick(Present());
        Assert.True(win.TribeWin);
        Assert.Equal(ValleyWarPhase.ScrollPending, win.Phase);

        TickMany(schedule, ValleyWarSchedule.ScrollDeleteDelayTicks, Present());
        Assert.Equal(ValleyWarPhase.BossWindow, schedule.Phase);
        return schedule;
    }

    private static ValleyWarSchedule AdvanceToPostWinCooldownStart()
    {
        var schedule = AdvanceToBossWindowStart();
        // BossSlotOccupied defaults to false -- the boss is never actually summoned in this cluster (see
        // ValleyWarSystem's own remarks), so the very first BossWindow tick always resolves as a win.
        var result = schedule.Tick(Present());
        Assert.True(result.BossWin);
        Assert.Equal(ValleyWarPhase.PostWinCooldown, schedule.Phase);
        return schedule;
    }

    [Fact]
    public void FreshSchedule_StartsIdle_NoWinningTribe()
    {
        var schedule = new ValleyWarSchedule();
        Assert.Equal(ValleyWarPhase.Idle, schedule.Phase);
        Assert.Null(schedule.WinningTribe);
    }

    [Fact]
    public void Idle_WaitsTheFullDuration_ThenEntersGateCountdown()
    {
        var schedule = new ValleyWarSchedule();
        TickMany(schedule, ValleyWarSchedule.IdleWaitTicks - 1);
        Assert.Equal(ValleyWarPhase.Idle, schedule.Phase);

        var result = schedule.Tick(ValleyWarEnvironmentSnapshot.Empty); // the exact IdleWaitTicks-th tick
        Assert.Equal(ValleyWarPhase.GateCountdown, result.Phase);
        Assert.Equal(ValleyWarPhase.Idle, result.PreviousPhase);
    }

    [Fact]
    public void GateCountdown_AnnouncesFiveDownToOne_ThenOpensTheGate()
    {
        var schedule = AdvanceToGateCountdownStart();

        var announced = new List<int>();
        for (var i = 0; i < ValleyWarSchedule.GateCountdownStartValue; i++)
        {
            var result = TickMany(schedule, ValleyWarSchedule.GateCountdownIntervalTicks);
            Assert.NotNull(result.GateCountdownValue);
            announced.Add(result.GateCountdownValue!.Value);
        }

        Assert.Equal([5, 4, 3, 2, 1], announced);
        Assert.Equal(ValleyWarPhase.GateCountdown, schedule.Phase); // not yet opened

        var openResult = TickMany(schedule, ValleyWarSchedule.GateCountdownIntervalTicks);
        Assert.True(openResult.GateOpened);
        Assert.Equal(ValleyWarPhase.GateOpen, openResult.Phase);
    }

    [Fact]
    public void GateOpen_WaitsOneMinute_ThenClosesAndEntersDoorPending()
    {
        var schedule = AdvanceToGateOpenStart();

        TickMany(schedule, ValleyWarSchedule.GateOpenTicks - 1);
        Assert.Equal(ValleyWarPhase.GateOpen, schedule.Phase);

        var result = schedule.Tick(ValleyWarEnvironmentSnapshot.Empty);
        Assert.True(result.GateClosed);
        Assert.Equal(ValleyWarPhase.DoorPending, result.Phase);
    }

    [Fact]
    public void DoorPending_CountsDownTenToOne_EverySecond_ThenOpensDoor_AndSeedsKillQuotas()
    {
        var schedule = AdvanceToDoorPendingStart();

        var countdowns = new List<int>();
        ValleyWarTickResult last = default;
        for (var i = 0; i < ValleyWarSchedule.DoorPendingTicks; i++)
        {
            last = schedule.Tick(ValleyWarEnvironmentSnapshot.Empty);
            if (last.DoorCountdownValue is { } value)
                countdowns.Add(value);
        }

        Assert.Equal([10, 9, 8, 7, 6, 5, 4, 3, 2, 1], countdowns);
        Assert.True(last.DoorOpened); // the 10th (last) countdown tick and the door-open tick coincide
        Assert.Equal(ValleyWarPhase.KillRace, last.Phase);

        for (byte t = 0; t < ValleyWarSchedule.TribeCount; t++)
            Assert.Equal(ValleyWarSchedule.KillQuotaPerTribeStart, schedule.GetKillQuota(t));
    }

    [Fact]
    public void KillRace_QuotaHitsZero_EntersScrollPending_RecordsWinningTribe()
    {
        var schedule = AdvanceToKillRaceStart();

        for (var i = 0; i < ValleyWarSchedule.KillQuotaPerTribeStart; i++)
            schedule.RegisterMonsterKill(2);

        var result = schedule.Tick(Present());
        Assert.True(result.TribeWin);
        Assert.Equal((byte)2, result.WinningTribe);
        Assert.True(result.MonstersShouldDespawn);
        Assert.Equal(ValleyWarPhase.ScrollPending, result.Phase);
        Assert.Equal((byte)2, schedule.WinningTribe);
    }

    [Fact]
    public void KillRace_NoEligiblePlayerPresent_EndsImmediately_EntersPreReset()
    {
        var schedule = AdvanceToKillRaceStart();

        var result = schedule.Tick(Present(false));
        Assert.True(result.KillRaceEndedEmptyOrTimeout);
        Assert.True(result.MonstersShouldDespawn);
        Assert.Null(result.WinningTribe);
        Assert.Equal(ValleyWarPhase.PreReset, result.Phase);
        Assert.Null(schedule.WinningTribe);
    }

    [Fact]
    public void KillRace_TimesOut_WithNoTribeEmptyingItsQuota_EntersPreReset()
    {
        var schedule = AdvanceToKillRaceStart();

        var result = TickMany(schedule, ValleyWarSchedule.KillRaceDurationTicks, Present());
        Assert.True(result.KillRaceEndedEmptyOrTimeout);
        Assert.Null(result.WinningTribe);
        Assert.Equal(ValleyWarPhase.PreReset, result.Phase);
    }

    [Fact]
    public void KillRace_QuotasBroadcastEveryTwoTicks_CountdownEveryTenTicks()
    {
        var schedule = AdvanceToKillRaceStart();

        for (var i = 1; i <= 9; i++)
        {
            var result = schedule.Tick(Present());
            Assert.Null(result.KillRaceCountdownValue);
            if (i % 2 == 0)
                Assert.NotNull(result.KillRaceQuotas);
            else
                Assert.Null(result.KillRaceQuotas);
        }

        var tenth = schedule.Tick(Present());
        Assert.NotNull(tenth.KillRaceQuotas);
        Assert.Equal([170, 170, 170, 170], tenth.KillRaceQuotas!.Value.ToArray());
        Assert.Equal(ValleyWarSchedule.KillRaceDurationTicks - 10, tenth.KillRaceCountdownValue);
    }

    [Fact]
    public void RegisterMonsterKill_OutsideKillRace_IsANoOp()
    {
        var schedule = AdvanceToKillRaceStart();
        Assert.Equal(170, schedule.GetKillQuota(1));

        schedule.Tick(Present(false)); // empty map -> PreReset (quota is NOT cleared until PreReset resolves)
        Assert.Equal(ValleyWarPhase.PreReset, schedule.Phase);

        schedule.RegisterMonsterKill(1); // no-op: Phase != KillRace
        Assert.Equal(170, schedule.GetKillQuota(1));
    }

    [Fact]
    public void RegisterMonsterKill_NeverGoesNegative()
    {
        var schedule = AdvanceToKillRaceStart();

        for (var i = 0; i < ValleyWarSchedule.KillQuotaPerTribeStart + 5; i++)
            schedule.RegisterMonsterKill(3);

        Assert.Equal(0, schedule.GetKillQuota(3));
    }

    [Fact]
    public void RegisterMonsterKill_InvalidTribeId_IsANoOp_OtherTribesUnaffected()
    {
        var schedule = AdvanceToKillRaceStart();

        schedule.RegisterMonsterKill(ValleyWarSchedule.TribeCount); // one past the valid range
        schedule.RegisterMonsterKill(255);

        for (byte t = 0; t < ValleyWarSchedule.TribeCount; t++)
            Assert.Equal(170, schedule.GetKillQuota(t));
    }

    [Fact]
    public void ForceZeroTribeQuota_WithinKillRace_ZeroesImmediately_WinDeterminedOnNextTick()
    {
        var schedule = AdvanceToKillRaceStart();

        schedule.ForceZeroTribeQuota(0);
        Assert.Equal(0, schedule.GetKillQuota(0));
        Assert.Equal(170, schedule.GetKillQuota(1)); // untouched

        var result = schedule.Tick(Present());
        Assert.True(result.TribeWin);
        Assert.Equal((byte)0, result.WinningTribe);
    }

    [Fact]
    public void ForceZeroTribeQuota_OutsideKillRace_IsANoOp()
    {
        var schedule = new ValleyWarSchedule(); // Idle
        schedule.ForceZeroTribeQuota(0);
        Assert.Equal(ValleyWarPhase.Idle, schedule.Phase); // unaffected, still Idle
    }

    [Fact]
    public void GetKillQuota_OutOfRangeTribeId_ReturnsZero()
    {
        var schedule = AdvanceToKillRaceStart(); // every real tribe seeded to 170
        Assert.Equal(0, schedule.GetKillQuota(ValleyWarSchedule.TribeCount));
        Assert.Equal(0, schedule.GetKillQuota(255));
    }

    [Fact]
    public void ScrollPending_WaitsThreeSeconds_ThenDeletesTheScroll_EntersBossWindow()
    {
        var schedule = AdvanceToKillRaceStart();
        schedule.ForceZeroTribeQuota(1);
        var win = schedule.Tick(Present());
        Assert.Equal(ValleyWarPhase.ScrollPending, win.Phase);

        TickMany(schedule, ValleyWarSchedule.ScrollDeleteDelayTicks - 1, Present());
        Assert.Equal(ValleyWarPhase.ScrollPending, schedule.Phase);

        var result = schedule.Tick(Present());
        Assert.True(result.BattleScrollDeleted);
        Assert.Equal(ValleyWarPhase.BossWindow, result.Phase);
    }

    [Fact]
    public void BossWindow_BossNeverSummoned_AlwaysWinsOnItsFirstTick()
    {
        var schedule = AdvanceToBossWindowStart();

        var result = schedule.Tick(Present(bossSlotOccupied: false));
        Assert.True(result.BossWin);
        Assert.Equal(ValleyWarPhase.PostWinCooldown, result.Phase);
    }

    [Fact]
    public void BossWindow_BossSlotReportedOccupied_TimesOutAfterTheFullDuration_EntersPreReset()
    {
        var schedule = AdvanceToBossWindowStart();
        var occupied = Present(bossSlotOccupied: true);

        var result = TickMany(schedule, ValleyWarSchedule.BossWindowDurationTicks, occupied);
        Assert.True(result.BossWindowTimeout);
        Assert.False(result.BossWin);
        Assert.Equal(ValleyWarPhase.PreReset, result.Phase);
    }

    [Fact]
    public void PostWinCooldown_WaitsOneMinute_ThenReturnsToTown_EntersPreReset()
    {
        var schedule = AdvanceToPostWinCooldownStart();

        TickMany(schedule, ValleyWarSchedule.PostWinCooldownTicks - 1, Present());
        Assert.Equal(ValleyWarPhase.PostWinCooldown, schedule.Phase);

        var result = schedule.Tick(Present());
        Assert.True(result.PostWinReturnToTown);
        Assert.True(result.MonstersShouldDespawn);
        Assert.Equal(ValleyWarPhase.PreReset, result.Phase);
    }

    [Fact]
    public void PreReset_WaitsOneMinute_ThenForceDisconnectsAndFullyResetsToIdle()
    {
        var schedule = AdvanceToKillRaceStart();
        var toPreReset = schedule.Tick(Present(false)); // empty-map end -> PreReset
        Assert.Equal(ValleyWarPhase.PreReset, toPreReset.Phase);

        TickMany(schedule, ValleyWarSchedule.PreResetTicks - 1, Present(false));
        Assert.Equal(ValleyWarPhase.PreReset, schedule.Phase);

        var result = schedule.Tick(Present(false));
        Assert.True(result.AllSessionsShouldDisconnect);
        Assert.Equal(ValleyWarPhase.Idle, result.Phase);
        Assert.Null(schedule.WinningTribe);
        for (byte t = 0; t < ValleyWarSchedule.TribeCount; t++)
            Assert.Equal(0, schedule.GetKillQuota(t));
    }
}
