using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>Where <see cref="TribeSymbolBattleScheduler" /> currently sits in its wait/countdown cycle.</summary>
public enum TribeSymbolBattleSchedulePhase : byte
{
    /// <summary>Waiting for the qualifying hour (and, outside test mode, day) to arrive.</summary>
    WaitingForHour,

    /// <summary>
    ///     The qualifying hour has been reached; counting simulated minutes toward the flag flip. Which
    ///     direction (opening vs. closing) is implied by <see cref="WorldStateService.World" />'s current
    ///     <see cref="WorldRvrState.TribeSymbolBattle" /> value at the moment the countdown was armed, not
    ///     stored separately -- same "purely flag-driven, no private marker" posture the legacy itself has.
    /// </summary>
    Counting
}

/// <summary>
///     Zone037's scheduled Tribe Symbol Battle open/close cycle: a fixed-hour (20:00 open / 22:00 close),
///     day-gated wait, a ten-simulated-minute "opens/closes in N minutes" countdown, one further simulated
///     minute of grace, then the flag flip -- reproduced here as one symmetric state machine driven purely by
///     the shared <see cref="WorldStateService.World" />.<see cref="WorldRvrState.TribeSymbolBattle" /> flag's
///     current value (closed =&gt; evaluate the opening half against hour 20; open =&gt; evaluate the closing half
///     against hour 22), exactly like the legacy: there is no private "already ran today" marker, so an
///     operator-triggered flip or a restart that leaves the flag already open is transparently picked up at
///     the next qualifying hour.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3632-3762 (full open/close state machine) ;
///     :2663-2694 (per-tick invocation gate) ; :578-622 (boot-time server-number==37 gate and enabling
///     configuration flag -- translated as <c>GameServerOptions.ShardId</c> == 37 by
///     <c>Fenrir.Application.Game.Hosting.World.ZoneWar.TribeSymbolBattleSchedulerHost</c>, this class's own
///     driver; this class itself has no shard-election concept, it simply always evaluates) ;
///     Server/ts25center/S04_MyWork02.cpp:364-372,398-407 (the two broadcast notices that flip
///     the shared flag on the receiving/center side -- collapsed onto
///     <see cref="ZoneEventBroadcaster.AnnounceTribeSymbolBattleStarted" />/
///     <see cref="ZoneEventBroadcaster.AnnounceTribeSymbolBattleEnded" />, which already both mutate
///     <see cref="WorldStateService" /> and broadcast sort 40/45 in one call) ; Server/Header/function.h:1642-1652
///     (tick-to-minute conversion, see <see cref="MinuteCountdown" />'s own remarks).
///     <para>
///         Each of the ten interim per-minute countdown steps broadcasts
///         <see cref="ZoneEventBroadcaster.AnnounceTribeSymbolBattleCountdown" />
///         (sort 39, "hsb countdown") -- a real, already-modeled wire notice, not a guess: it carries no
///         minutes-remaining payload (matching the legacy's own bare <c>Broadcast(39)</c> call), so this is a
///         pulse only, with the actual minute count logged locally alongside it.
///     </para>
///     <para>
///         GAP -- not modeled, deliberately not guessed: (1) the closing step's "second companion notice"
///         alongside the sort-45 broadcast has no cited wire sort/opcode in the translated behavior contract,
///         so it is logged only -- a real client-facing broadcast needs a fresh legacy-behavior-translator/
///         wire-protocol finding for the exact sort number before it can be wired in; (2) the "four per-tribe
///         master-call-ability counters reset to zero" on close has no backing field anywhere in
///         <see cref="WorldStateService" />/<see cref="PlayerRuntimeState" /> -- needs its own contract
///         identifying what this counter represents and where it should be persisted before it can be
///         implemented; (3) the exact three days-of-week the legacy restricts this to are not named by the
///         translated contract -- <see cref="AllowedDays" /> defaults to empty (see its own remarks) rather
///         than guessing which three days.
///     </para>
/// </remarks>
public sealed class TribeSymbolBattleScheduler(
    WorldStateService worldState,
    ZoneEventBroadcaster broadcaster,
    ILogger<TribeSymbolBattleScheduler> logger,
    IReadOnlySet<DayOfWeek>? allowedDays = null,
    bool testMode = false)
{
    /// <summary>Fixed opening hour, 8 PM local server time (S07_MyGame01.cpp:3632-3762's three identical build branches).</summary>
    public const int OpenHour = 20;

    /// <summary>Fixed closing hour, 10 PM local server time -- same three-branch duplication, same values.</summary>
    public const int CloseHour = 22;

    /// <summary>Ten simulated minutes of "opens/closes in N minutes" countdown steps, N = 10 down to 1.</summary>
    public const int CountdownMinutes = 10;

    private readonly MinuteCountdown _countdown = new();

    /// <summary>
    ///     Which three days of the week the legacy restricts this behavior to outside test mode -- NOT named
    ///     by the translated behavior contract (only "one of three fixed days" is stated). Defaults to empty:
    ///     fails safe (the schedule simply never fires outside <see cref="testMode" /> until this is
    ///     configured with the real three days) rather than guessing and risking the schedule silently firing
    ///     on the wrong day of the week.
    /// </summary>
    public IReadOnlySet<DayOfWeek> AllowedDays { get; } = allowedDays ?? new HashSet<DayOfWeek>();

    public TribeSymbolBattleSchedulePhase Phase { get; private set; } = TribeSymbolBattleSchedulePhase.WaitingForHour;

    /// <summary>
    ///     1-10 once at least one simulated minute has elapsed while <see cref="Phase" /> is
    ///     <see cref="TribeSymbolBattleSchedulePhase.Counting" />; 0 both outside that phase and in the instant
    ///     right after arming, before the first whole minute (and its sort-39 pulse) has actually elapsed.
    /// </summary>
    public int MinutesRemainingInCountdown =>
        Phase != TribeSymbolBattleSchedulePhase.Counting ||
        _countdown.MinutesElapsed is 0 or > CountdownMinutes
            ? 0
            : CountdownMinutes + 1 - _countdown.MinutesElapsed;

    /// <summary>
    ///     One scheduler evaluation. <paramref name="utcNow" /> drives the hour/day gate (re-read fresh every
    ///     call, matching the legacy's own per-tick clock read); <paramref name="elapsed" /> is the real time
    ///     since the previous call, accumulated toward the next simulated-minute countdown step.
    /// </summary>
    public void Tick(TimeSpan elapsed, DateTime utcNow)
    {
        switch (Phase)
        {
            case TribeSymbolBattleSchedulePhase.WaitingForHour:
                EvaluateWaiting(utcNow);
                break;
            case TribeSymbolBattleSchedulePhase.Counting:
                AdvanceCountdown(elapsed);
                break;
        }
    }

    private void EvaluateWaiting(DateTime utcNow)
    {
        var closing = worldState.World.TribeSymbolBattle;
        var targetHour = closing ? CloseHour : OpenHour;

        if (utcNow.Hour != targetHour)
            return;

        if (!testMode && !AllowedDays.Contains(utcNow.DayOfWeek))
            return;

        Phase = TribeSymbolBattleSchedulePhase.Counting;
        _countdown.Reset();

        logger.LogInformation(
            "TribeSymbolBattle: qualifying hour {Hour} reached ({Direction}) -- countdown armed", targetHour,
            closing ? "closing" : "opening");
    }

    private void AdvanceCountdown(TimeSpan elapsed)
    {
        var wholeMinutes = _countdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
            OnMinuteElapsed();
    }

    private void OnMinuteElapsed()
    {
        var closing = worldState.World.TribeSymbolBattle;
        var minute = _countdown.MinutesElapsed;

        if (minute is >= 1 and <= CountdownMinutes)
        {
            var minutesRemaining = CountdownMinutes + 1 - minute;
            logger.LogInformation("TribeSymbolBattle {Direction} in {MinutesRemaining} minute(s)",
                closing ? "closes" : "opens", minutesRemaining);
            broadcaster.AnnounceTribeSymbolBattleCountdown();
            return;
        }

        if (minute != CountdownMinutes + 1)
            return;

        if (closing)
            broadcaster.AnnounceTribeSymbolBattleEnded();
        else
            broadcaster.AnnounceTribeSymbolBattleStarted();

        // GAP: the "four per-tribe master-call-ability counters" reset on close has no backing field yet --
        // see class remarks.
        Phase = TribeSymbolBattleSchedulePhase.WaitingForHour;
        _countdown.Reset();
    }
}
