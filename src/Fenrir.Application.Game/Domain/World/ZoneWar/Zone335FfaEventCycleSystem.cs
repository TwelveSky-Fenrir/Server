using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum Zone335FfaPhase : byte
{
    Idle,

    CountdownArmed,

    PreStartCountdown,

    GateOpenPending,

    EntranceOpen,

    BattlePrep,

    Battle,

    PostBattleCleanupPending,

    WindDown
}

public sealed class Zone335FfaEventCycleSystem(
    ZoneCenterSiegeState siegeState,
    Zone335StartTrigger startTrigger,
    Lazy<ZoneEventBroadcaster> broadcaster,
    ILogger<Zone335FfaEventCycleSystem> logger) : ISimulationSystem
{
    public const int PreStartCountdownMinutes = 10;

    public const int GateOpenWaitMinutes = 1;

    public const int EntranceOpenWindowMinutes = 1;

    public const int BattlePrepWaitMinutes = 1;

    public const int DefaultBattleDurationLegacyTicks = 2400;

    public const int LiveCountdownBroadcastCadenceLegacyTicks = 10;

    public const int PostBattleCleanupWaitMinutes = 1;

    public const int WindDownWaitMinutes = 1;

    public const int BattlePrepLegacyTicks = 120;

    public const int BattlePrepCountdownStartSeconds = 60;

    public const int BattlePrepCountdownCadenceLegacyTicks = 10;

    private readonly MinuteCountdown _minuteCountdown = new();

    private int _battleDurationLegacyTicks = DefaultBattleDurationLegacyTicks;
    private int _battlePrepTicksElapsed;
    private int _battleTicksRemaining;
    private int _liveCountdownTicksSinceLastBroadcast;

    public Zone335FfaPhase Phase { get; private set; } = Zone335FfaPhase.Idle;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0 || zone.MapId != PvpKillRewardZoneCatalog.FfaMapNumber)
            return;

        var elapsed = SimulationClock.ToTimeSpan(legacyTicksElapsed);

        switch (Phase)
        {
            case Zone335FfaPhase.Idle:
                AdvanceIdle(zone);
                break;
            case Zone335FfaPhase.CountdownArmed:
                AdvanceCountdownArmed();
                break;
            case Zone335FfaPhase.PreStartCountdown:
                AdvancePreStartCountdown(elapsed);
                break;
            case Zone335FfaPhase.GateOpenPending:
                AdvanceGateOpenPending(elapsed);
                break;
            case Zone335FfaPhase.EntranceOpen:
                AdvanceEntranceOpen(elapsed);
                break;
            case Zone335FfaPhase.BattlePrep:
                AdvanceBattlePrep(elapsed, zone, legacyTicksElapsed);
                break;
            case Zone335FfaPhase.Battle:
                AdvanceBattle(zone, legacyTicksElapsed);
                break;
            case Zone335FfaPhase.PostBattleCleanupPending:
                AdvancePostBattleCleanupPending(elapsed);
                break;
            case Zone335FfaPhase.WindDown:
                AdvanceWindDown(elapsed, zone);
                break;
        }
    }

    private void AdvanceIdle(Zone zone)
    {
        if (!startTrigger.TryConsumeStartRequest(out var requestedBattleTicks))
            return;

        _battleDurationLegacyTicks =
            requestedBattleTicks > 0 ? requestedBattleTicks : DefaultBattleDurationLegacyTicks;

        zone.ClearKillFeedLeaderboard();
        siegeState.ResetTribeBonusFields();

        logger.LogInformation(
            "Zone335Ffa: GM start request accepted -- countdown-arm phase entered, battle timer {BattleDurationLegacyTicks} legacy tick(s)",
            _battleDurationLegacyTicks);

        Phase = Zone335FfaPhase.CountdownArmed;
    }

    private void AdvanceCountdownArmed()
    {
        _minuteCountdown.Reset();
        Phase = Zone335FfaPhase.PreStartCountdown;
    }

    private void AdvancePreStartCountdown(TimeSpan elapsed)
    {
        var wholeMinutes = _minuteCountdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
        {
            var minute = _minuteCountdown.MinutesElapsed;

            if (minute <= PreStartCountdownMinutes)
            {
                var remaining = PreStartCountdownMinutes - minute + 1;
                broadcaster.Value.AnnounceFfaCountdown(remaining);
                continue;
            }

            if (minute != PreStartCountdownMinutes + 1)
                continue;

            _minuteCountdown.Reset();
            Phase = Zone335FfaPhase.GateOpenPending;
            return;
        }
    }

    private void AdvanceGateOpenPending(TimeSpan elapsed)
    {
        var wholeMinutes = _minuteCountdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
        {
            if (_minuteCountdown.MinutesElapsed < GateOpenWaitMinutes)
                continue;

            broadcaster.Value.AnnounceFfaGateOpen();
            _minuteCountdown.Reset();
            Phase = Zone335FfaPhase.EntranceOpen;
            return;
        }
    }

    private void AdvanceEntranceOpen(TimeSpan elapsed)
    {
        var wholeMinutes = _minuteCountdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
        {
            if (_minuteCountdown.MinutesElapsed < EntranceOpenWindowMinutes)
                continue;

            broadcaster.Value.AnnounceFfaEntranceOpen();
            _minuteCountdown.Reset();
            _battlePrepTicksElapsed = 0;
            Phase = Zone335FfaPhase.BattlePrep;
            return;
        }
    }

    private void AdvanceBattlePrep(TimeSpan elapsed, Zone zone, int legacyTicksElapsed)
    {
        for (var tick = 0; tick < legacyTicksElapsed; tick++)
        {
            _battlePrepTicksElapsed++;
            if (_battlePrepTicksElapsed >= BattlePrepLegacyTicks)
                break;

            if (_battlePrepTicksElapsed == 1)
            {
                BroadcastBattlePrepCountdown(zone, BattlePrepCountdownStartSeconds);
            }
            else if (_battlePrepTicksElapsed % BattlePrepCountdownCadenceLegacyTicks == 0)
            {
                var elapsedSeconds = _battlePrepTicksElapsed / SimulationClock.OneSecondGateLegacyTicks;
                BroadcastBattlePrepCountdown(zone, BattlePrepCountdownStartSeconds - elapsedSeconds);
            }
        }

        var wholeMinutes = _minuteCountdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
        {
            if (_minuteCountdown.MinutesElapsed < BattlePrepWaitMinutes)
                continue;

            _battleTicksRemaining = _battleDurationLegacyTicks;
            _liveCountdownTicksSinceLastBroadcast = 0;
            _battlePrepTicksElapsed = 0;

            broadcaster.Value.AnnounceFfaBattleStart(_battleTicksRemaining);
            Phase = Zone335FfaPhase.Battle;
            return;
        }
    }

    private static void BroadcastBattlePrepCountdown(Zone zone, int remainingSeconds)
    {
        var response = new ZoneWar335CountdownResponse { RemainTime = remainingSeconds };
        foreach (var player in zone.Players)
            if (!player.IsMovingZone)
                player.Session.Send(response);
    }

    private void AdvanceBattle(Zone zone, int legacyTicksElapsed)
    {
        if (CountEligible(zone) == 0)
        {
            broadcaster.Value.AnnounceFfaClosedNotice();
            _minuteCountdown.Reset();
            Phase = Zone335FfaPhase.WindDown;
            return;
        }

        _battleTicksRemaining = Math.Max(0, _battleTicksRemaining - legacyTicksElapsed);
        _liveCountdownTicksSinceLastBroadcast += legacyTicksElapsed;

        if (_battleTicksRemaining > 0)
        {
            if (_liveCountdownTicksSinceLastBroadcast < LiveCountdownBroadcastCadenceLegacyTicks)
                return;

            _liveCountdownTicksSinceLastBroadcast = 0;
            BroadcastLiveCountdown(zone, _battleTicksRemaining);
            return;
        }

        zone.ApplyKillFeedEndOfBattleRewards(true, false);
        broadcaster.Value.AnnounceFfaBattleEnd();

        _minuteCountdown.Reset();
        Phase = Zone335FfaPhase.PostBattleCleanupPending;
    }

    private void AdvancePostBattleCleanupPending(TimeSpan elapsed)
    {
        var wholeMinutes = _minuteCountdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
        {
            if (_minuteCountdown.MinutesElapsed < PostBattleCleanupWaitMinutes)
                continue;

            broadcaster.Value.AnnounceFfaClosedNotice();
            _minuteCountdown.Reset();
            Phase = Zone335FfaPhase.WindDown;
            return;
        }
    }

    private void AdvanceWindDown(TimeSpan elapsed, Zone zone)
    {
        var wholeMinutes = _minuteCountdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
        {
            if (_minuteCountdown.MinutesElapsed < WindDownWaitMinutes)
                continue;

            FinalizeReset(zone);
            return;
        }
    }

    private void FinalizeReset(Zone zone)
    {
        ForceReturnEligiblePlayers(zone);

        zone.ClearKillFeedLeaderboard();

        broadcaster.Value.AnnounceFfaReset();

        Phase = Zone335FfaPhase.Idle;
        _battleTicksRemaining = 0;
        _battleDurationLegacyTicks = DefaultBattleDurationLegacyTicks;
        _liveCountdownTicksSinceLastBroadcast = 0;
        _battlePrepTicksElapsed = 0;
        _minuteCountdown.Reset();
    }

    private static void BroadcastLiveCountdown(Zone zone, int remainingLegacyTicks)
    {
        var response = new ZoneWarFfaBattleInfoResponse { RemainTime = remainingLegacyTicks };
        foreach (var player in zone.Players)
            if (!player.IsMovingZone)
                player.Session.Send(response);
    }

    private static void ForceReturnEligiblePlayers(Zone zone)
    {
        foreach (var player in zone.Players)
        {
            if (player.IsMovingZone)
                continue;

            player.Session.Send(new ReturnToHomeZoneResponse());
        }
    }

    private static int CountEligible(Zone zone)
    {
        var count = 0;
        foreach (var player in zone.Players)
            if (!player.IsMovingZone && player.VisibleState != 0)
                count++;

        return count;
    }
}
