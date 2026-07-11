using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Where <see cref="Zone335FfaEventCycleSystem" /> currently sits, finer-grained than the six values
///     the shared, cluster-wide <see cref="ZoneCenterSiegeState.Zone335" /> scalar itself distinguishes (three of
///     these -- <see cref="Idle" />/<see cref="CountdownArmed" />/<see cref="PreStartCountdown" /> -- all keep that
///     scalar at 0; the client-visible scalar only starts moving at <see cref="GateOpenPending" />'s own exit).
/// </summary>
public enum Zone335FfaPhase : byte
{
    /// <summary>Accumulating toward the 60-in-game-minute automatic rollover, or waiting for a GM start request.</summary>
    Idle,

    /// <summary>Exactly one tick: arms the 10-minute pre-start countdown.</summary>
    CountdownArmed,

    /// <summary>Counting the 10-minute "opens in N minute(s)" countdown, broadcasting once per minute.</summary>
    PreStartCountdown,

    /// <summary>One further in-game minute, then broadcasts the gate-open notice and advances.</summary>
    GateOpenPending,

    /// <summary>The 2-in-game-minute entrance-open window.</summary>
    EntranceOpen,

    /// <summary>One further in-game minute, then arms the battle timer and advances.</summary>
    BattlePrep,

    /// <summary>The battle itself -- ends on last-man-standing (after the opening minute) or timer expiry.</summary>
    Battle,

    /// <summary>One further in-game minute, then the post-battle cleanup pass runs and advances.</summary>
    PostBattleCleanupPending,

    /// <summary>Re-announces the closed notice, then performs the final reset back to <see cref="Idle" />.</summary>
    WindDown
}

/// <summary>
///     Zone 335's Free-For-All event tick -- legacy <c>Process_Zone_335_FFA</c>: consumes
///     <see cref="Zone335StartTrigger" />'s armed "start requested" flag, drives
///     <see cref="ZoneCenterSiegeState.Zone335" /> through its 6 client-visible values (0 idle / 1 gate-open /
///     2 entrance-open / 3 battle / 4 battle-just-ended / 5 closed) across the full 9-phase cycle described by
///     <see cref="Zone335FfaPhase" />, and broadcasts each transition server-wide via
///     <see cref="ZoneEventBroadcaster" />'s <c>AnnounceFfa*</c> methods.
///     <para>
///         A per-zone <see cref="Simulation.ISimulationSystem" /> rather than a separate
///         <c>BackgroundService</c>/host (unlike <c>HolyStoneWarCycle</c>/<c>HolyStoneWarCycleHost</c>, which
///         need one because the Holy Stone's site isn't a whole zone's own identity): this system's very first
///         line already gates on <c>zone.MapId == FFAMAPNUM</c>, so it naturally runs real work on exactly the
///         one shard/zone that hosts map 335 (Fenrir's own map-partitioning already guarantees at most one such
///         zone exists cluster-wide) and is a free no-op everywhere else -- reproducing the legacy's own
///         boot-time "is this process server number 335" gate (<c>S07_MyGame01.cpp:1101-1113</c>) without a
///         second timer/host loop. Since <see cref="Simulation.ISimulationSystem" /> is registered once as a
///         process-wide singleton but only ever receives real (non-early-return) calls from the one zone's own
///         single-threaded tick loop, the phase/timer fields below need no additional locking of their own.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:10736-11006 (full state machine body) ; :2981-2987 (per-tick
///     dispatch gate) ; :1101-1113 (process-identity flag) ; :1421-1424 (force-reset to 0 on boot) ;
///     Server/ts25zone/H07_MyGame.h:35 (<c>ZONE335</c> unconditionally compiled) ;
///     Server/Header/Protocol/DEFINE.h:99 (<c>FFAMAPNUM</c> = 335) ; Server/Header/function.h:1642-1646 (120
///     legacy ticks per in-game minute) ; Server/ts25zone/S04_MyWork04.cpp:1097-1131 (GM start command arming
///     the trigger this system consumes) ; Server/ts25zone/S07_MyGame01.cpp:10757-10767 (idle/60-minute
///     rollover, kill-tracking + 4-field reset on exit) ; :10780-10787 (countdown-arm) ; :10788-10810 (pre-start
///     countdown) ; :10811-10820 (gate-open transition) ; :10823-10832 (entrance-open window) ; :10833-10850
///     (battle-start preparation, battle timer set to 1800) ; :10851-10944 (battle body: last-man-standing +
///     timer-expiry end conditions, :10874/:10906-10912 live countdown cadence) ; :10945-10961 (post-battle
///     cleanup) ; :10963-11004 (wind-down + final reset).
///     <para>
///         <b>Deliberately out of scope for this system, and why:</b> the combat-triggered per-kill mechanics
///         phase 7 also names -- the war-point/PK-point reward and its 3-minute same-victim cooldown
///         (<c>S07_MyGame02.cpp:125-170</c>), the item-drop roll gated on combined level &gt;= 145
///         (<c>S07_MyGame03.cpp:3876-3989</c>), and the kill-feed broadcast plus phase-3-gated top-3 leaderboard
///         (<c>S07_MyGame02.cpp:312-551</c>) -- all fire from the attack/kill-resolution path (<c>Zone.Combat.cs</c>),
///         not from this tick, and need their own follow-up wiring pass there; this system only reads/writes
///         the shared phase scalar those hooks would gate on, it never grants a kill reward itself. Three
///         further steps are real, cited, but no-ops today for lack of any backing structure/mechanic anywhere
///         in Fenrir yet, each flagged at its own call site below rather than silently skipped: the kill-
///         tracking/leaderboard array clear (phases 1/8/9 -- no per-character kill-count structure exists,
///         see the GAP above), the cosmetic-equipment snapshot/apply/restore (phases 6/8/9 -- confirmed inert
///         in production regardless, every one of the six item ids is hardcoded to 0 in legacy source and the
///         per-slot apply routine no-ops for id&lt;=0), and the FFA-only summoned-monster despawn (phase 8 -- no
///         FFA-specific monster-summon mechanic exists to despawn). The end-of-battle top-1/2/3 CP reward
///         (phase 7's own end) is likewise a no-op for the same missing-leaderboard reason.
///     </para>
///     <para>
///         "Ready"/"hiding" are not modeled anywhere in <see cref="PlayerRuntimeState" /> today -- every roster
///         computation below (eligible-count, live-countdown recipients, forced-return recipients) only filters
///         on <see cref="PlayerRuntimeState.IsMovingZone" />, the sole eligibility predicate this codebase
///         actually has, matching the exact convention <c>RegularWarAfkTickSystem</c> already established.
///     </para>
///     <para>
///         Phase 5/6 relative timing is under-specified by the source contract (phase 6's own "Trigger" field
///         reads "1 in-game minute after entering phase 5," which cannot be literally correct once phase 5's
///         own 2-minute duration is also honored -- entering phase 6 already happens 2 minutes after phase 5
///         starts). This class resolves the ambiguity by treating every phase's own "Duration/end condition"
///         wait as measured from THAT phase's own entry, not chained to a different phase's start -- a
///         deliberate, documented engineering call, not a re-derived legacy fact; flag for
///         <c>cpp-zone-gameplay-analyst</c> re-check if byte-exact phase-5/6 timing ever matters.
///     </para>
/// </remarks>
public sealed class Zone335FfaEventCycleSystem(
    ZoneCenterSiegeState siegeState,
    Zone335StartTrigger startTrigger,
    Lazy<ZoneEventBroadcaster> broadcaster,
    ILogger<Zone335FfaEventCycleSystem> logger) : ISimulationSystem
{
    /// <summary>60 in-game minutes (Server/ts25zone/S07_MyGame01.cpp:10757-10767).</summary>
    public const int IdleAutoStartLegacyTicks = 60 * SimulationClock.PlayTimeAccrualLegacyTicks;

    /// <summary>10-minute "opens in N minute(s)" countdown (:10788-10810).</summary>
    public const int PreStartCountdownMinutes = 10;

    /// <summary>One further minute before the gate-open broadcast (:10811-10820).</summary>
    public const int GateOpenWaitMinutes = 1;

    /// <summary>2-minute entrance-open window (:10823-10832).</summary>
    public const int EntranceOpenWindowMinutes = 2;

    /// <summary>One further minute before battle-start preparation fires (:10833-10850).</summary>
    public const int BattlePrepWaitMinutes = 1;

    /// <summary>
    ///     Fixed 15-minute/1800-tick battle timer -- overwrites whatever the GM command's duration parameter set
    ///     (:10841-10844).
    /// </summary>
    public const int BattleDurationLegacyTicks = 1800;

    /// <summary>
    ///     Last-man-standing may not end the battle before this many ticks have elapsed (guards the opening tick's
    ///     not-yet-populated roster, :10874).
    /// </summary>
    public const int BattleMinimumElapsedLegacyTicksForLastManStanding = SimulationClock.PlayTimeAccrualLegacyTicks;

    /// <summary>Live in-battle countdown broadcast cadence: every 10 ticks/5 s (:10906-10912).</summary>
    public const int LiveCountdownBroadcastCadenceLegacyTicks = 10;

    /// <summary>One further minute before the post-battle cleanup pass fires (:10945-10961).</summary>
    public const int PostBattleCleanupWaitMinutes = 1;

    /// <summary>One further minute before the final wind-down reset fires (:10963-11004).</summary>
    public const int WindDownWaitMinutes = 1;

    private readonly MinuteCountdown _minuteCountdown = new();

    private int _battleElapsedLegacyTicks;
    private int _battleTicksRemaining;
    private int _idleElapsedLegacyTicks;
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
                AdvanceIdle(zone, legacyTicksElapsed);
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
                AdvanceBattlePrep(elapsed);
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

    private void AdvanceIdle(Zone zone, int legacyTicksElapsed)
    {
        var skipWait = startTrigger.ConsumeStartRequest();

        if (!skipWait)
        {
            _idleElapsedLegacyTicks += legacyTicksElapsed;
            if (_idleElapsedLegacyTicks < IdleAutoStartLegacyTicks)
                return;
        }

        _idleElapsedLegacyTicks = 0;

        // C15: kill-feed leaderboard clear (source contract step 12).
        zone.ClearKillFeedLeaderboard();
        siegeState.ResetTribeBonusFields();

        logger.LogInformation(
            "Zone335Ffa: idle wait ended ({Reason}) -- countdown-arm phase entered",
            skipWait ? "GM start request" : "60-minute automatic rollover");

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
            Phase = Zone335FfaPhase.BattlePrep;
            return;
        }
    }

    private void AdvanceBattlePrep(TimeSpan elapsed)
    {
        var wholeMinutes = _minuteCountdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
        {
            if (_minuteCountdown.MinutesElapsed < BattlePrepWaitMinutes)
                continue;

            _battleTicksRemaining = BattleDurationLegacyTicks;
            _battleElapsedLegacyTicks = 0;
            _liveCountdownTicksSinceLastBroadcast = 0;

            // GAP: the cosmetic-equipment "apply a fixed six-slot set" step is a documented no-op in production
            // -- see class remarks -- not reproduced here since it would change nothing observable.

            broadcaster.Value.AnnounceFfaBattleStart(_battleTicksRemaining);
            Phase = Zone335FfaPhase.Battle;
            return;
        }
    }

    private void AdvanceBattle(Zone zone, int legacyTicksElapsed)
    {
        _battleElapsedLegacyTicks += legacyTicksElapsed;
        _battleTicksRemaining = Math.Max(0, _battleTicksRemaining - legacyTicksElapsed);
        _liveCountdownTicksSinceLastBroadcast += legacyTicksElapsed;

        if (_battleTicksRemaining > 0 &&
            _liveCountdownTicksSinceLastBroadcast >= LiveCountdownBroadcastCadenceLegacyTicks)
        {
            _liveCountdownTicksSinceLastBroadcast = 0;
            BroadcastLiveCountdown(zone, _battleTicksRemaining);
        }

        var pastOpeningGuard =
            _battleElapsedLegacyTicks > BattleMinimumElapsedLegacyTicksForLastManStanding;
        var lastManStanding = pastOpeningGuard && CountEligible(zone) <= 1;
        var timerExpired = _battleTicksRemaining <= 0;

        if (!lastManStanding && !timerExpired)
            return;

        // C15: end-of-battle top-1/2/3 CP reward (source contract steps 9-11), FFA table.
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

            // GAP: equipment-snapshot restore, kill-tracking/per-user-reward-tracking clear, and FFA-summoned-
            // monster despawn are all no-ops here -- see class remarks (none has a backing structure/mechanic
            // in Fenrir yet, and the cosmetic-equipment feature is inert in production regardless).

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
            {
                // Re-announce the closed notice while still waiting -- only reachable if WindDownWaitMinutes is
                // ever raised above 1; matches the contract's "once per minute while waiting" framing.
                broadcaster.Value.AnnounceFfaClosedNotice();
                continue;
            }

            FinalizeReset(zone);
            return;
        }
    }

    private void FinalizeReset(Zone zone)
    {
        // GAP: equipment-restore repeat is a no-op here -- see BattlePrep's own remarks.
        ForceReturnEligiblePlayers(zone);

        // C15: kill-feed leaderboard clear (source contract step 12).
        zone.ClearKillFeedLeaderboard();

        broadcaster.Value.AnnounceFfaReset();

        Phase = Zone335FfaPhase.Idle;
        _idleElapsedLegacyTicks = 0;
        _battleTicksRemaining = 0;
        _battleElapsedLegacyTicks = 0;
        _liveCountdownTicksSinceLastBroadcast = 0;
        _minuteCountdown.Reset();
    }

    /// <summary>
    ///     Local-only (this zone's own connected roster), NOT the cluster-wide fan-out the phase-transition
    ///     <c>AnnounceFfa*</c> broadcasts use -- the source contract's own "Notification delivery scope" section
    ///     only covers the 1501-1507 event-code family relayed through the center process; this periodic op200
    ///     countdown (a different wire message entirely) has no such citation, so it is scoped narrowly to
    ///     players physically on map 335 rather than assumed to share the same server-wide reach.
    /// </summary>
    private static void BroadcastLiveCountdown(Zone zone, int remainingLegacyTicks)
    {
        var response = new ZoneWar335CountdownResponse { RemainTime = remainingLegacyTicks };
        foreach (var player in zone.Players)
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
            if (!player.IsMovingZone)
                count++;

        return count;
    }
}
