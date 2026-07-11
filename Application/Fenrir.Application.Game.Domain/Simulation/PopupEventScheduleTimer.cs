using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     The hour/minute-gated timer that decides WHEN a popup-event window is open (arms
///     <see cref="PopupEventState" />), the missing "follow-up" <see cref="PopupEventRewardSystem" />'s own
///     remarks flagged as a gap: "Nothing toggles it yet -- a follow-up must wire it from a real source." This
///     class is that source for three of the five <see cref="PopupEventType" /> values --
///     <see cref="PopupEventType.YanggokPvp" />, <see cref="PopupEventType.MonsterPve" />, and
///     <see cref="PopupEventType.InvasionPvp" />. <see cref="PopupEventType.RegularWar" />/
///     <see cref="PopupEventType.RuinsPvp" /> are OUT OF SCOPE here -- the A12 contract's own citations never
///     cover how those two arm (they belong to the Regular War / Zone267 Ruins siege mechanisms), so this
///     class never calls <c>SetEnabled</c> for either.
/// </summary>
/// <remarks>
///     Réf. C++ (via the A12 scheduled-events behavior contract) : Server/ts25zone/S07_MyGame01.cpp:2678-2689
///     (both timers' per-tick invocation, unconditionally compiled) ; :4553-4690 (PvP/PvE popup timer --
///     Yanggok opens hour 0/14/18, countdown at minute 49/54/58 with remaining 10/5/1, opens at minute 59 top
///     of the minute, closes at hour 2/16/20; Monster/PvE opens hour 1/11 with the identical countdown
///     pattern, opens at minute 59, closes at hour 3/13) ; :4692-4761 (invasion popup timer -- opens hour
///     12/21, closes hour 14/23) ; :486-564 (both timers' own shard gate -- the PvP/PvE gate is confirmed
///     VACUOUS, i.e. true on every zone shard; the invasion gate is genuinely selective to four specific
///     server numbers the contract does NOT give numerically). Kill thresholds for each window (10/0
///     Yanggok, 0/400 Monster, 5/0 Invasion) are already fully modeled by
///     <see cref="PopupEventZoneCatalog.KillThreshold" />/<see cref="PopupEventZoneCatalog.MonsterKillThreshold" />
///     -- this class only flips the on/off flag, never re-derives a threshold.
///     <para>
///         GATE DECISION: since the invasion timer's real "four specific server numbers" gate is never given
///         numerically by the contract (never invent an id), and since <see cref="PopupEventState" /> is a
///         per-shard-process singleton while <see cref="PopupEventRewardSystem.NotifyPvpKill" /> already
///         independently re-checks <see cref="PopupEventZoneCatalog.TryResolvePvpType" /> (a DIFFERENT,
///         already-fully-specified per-kill map gate, 1/6/11/140) before ever counting a kill, arming the
///         Invasion flag on every shard unconditionally -- same as the PvP/PvE timer's own confirmed-vacuous
///         gate -- has NO observable behavioral difference from restricting it to the real four servers: a
///         shard hosting none of those relevant maps just flips a flag nobody's own per-kill gate will ever
///         consult. See openQuestions for the flag-value case if this reasoning is ever revisited.
///     </para>
///     <para>
///         EDGE-TRIGGERED, not literally the legacy's "several independent countdown-minute checks re-run
///         every tick" shape: <see cref="Tick" /> only evaluates transitions on the first call that observes a
///         new minute-of-day value (never re-fires within the same minute regardless of poll frequency), the
///         same idiom <see cref="World.ZoneWar.DailyResetBroadcastScheduler" /> uses for its own one-shot
///         daily transition.
///     </para>
///     <para>
///         CLOSE-MINUTE INFERENCE: the contract gives an explicit MINUTE for every OPEN/countdown transition
///         (49/54/58/59) but only an HOUR for every CLOSE transition ("closes at hour two, sixteen, or
///         twenty"). This class closes at minute 0 of the stated close hour (top-of-hour) as its own
///         documented convention -- not an invented legacy value, just the natural reading of an
///         hour-only-given transition, flagged in openQuestions in case a future citation supplies an exact
///         close minute.
///     </para>
///     <para>
///         NOT SENT: the client-facing countdown/open/close notices themselves. The contract confirms the raw
///         event still reaches clients regardless of whether the zone-side reaction is alive, but never gives
///         a numeric wire sort code for any of these three notices (the "cases 1510-1514" range this
///         contract's own title associates with the popup system turns out, on the contract's own citations,
///         to be almost entirely dead zone-side reactions plus ONE code -- 1510 -- that is actually the
///         UNRELATED Zone038 tribe-wide DTM effect <see cref="World.ZoneWar.ZoneCenterBroadcastIngestor.DtmEventCode" />
///         already owns, not a popup notice). Never invent an opcode -- see openQuestions.
///     </para>
/// </remarks>
public sealed class PopupEventScheduleTimer(PopupEventState state, ILogger<PopupEventScheduleTimer> logger)
{
    private static readonly int[] YanggokOpenHours = [0, 14, 18];
    private static readonly int[] YanggokCloseHours = [2, 16, 20];
    private static readonly int[] MonsterOpenHours = [1, 11];
    private static readonly int[] MonsterCloseHours = [3, 13];
    private static readonly int[] InvasionOpenHours = [12, 21];
    private static readonly int[] InvasionCloseHours = [14, 23];

    /// <summary>(minute, remaining-count) pairs shared by the Yanggok and Monster countdown windows.</summary>
    private static readonly (int Minute, int Remaining)[] CountdownSchedule = [(49, 10), (54, 5), (58, 1)];

    private int _lastMinuteOfDay = -1;

    /// <summary>Call from a periodic host at sub-minute granularity; a no-op unless the minute-of-day just changed.</summary>
    public void Tick(DateTime utcNow)
    {
        var minuteOfDay = utcNow.Hour * 60 + utcNow.Minute;
        if (minuteOfDay == _lastMinuteOfDay)
            return;

        _lastMinuteOfDay = minuteOfDay;

        var hour = utcNow.Hour;
        var minute = utcNow.Minute;

        ProcessCountdownWindow(PopupEventType.YanggokPvp, YanggokOpenHours, YanggokCloseHours, hour, minute);
        ProcessCountdownWindow(PopupEventType.MonsterPve, MonsterOpenHours, MonsterCloseHours, hour, minute);
        ProcessSimpleWindow(PopupEventType.InvasionPvp, InvasionOpenHours, InvasionCloseHours, hour, minute);
    }

    private void ProcessCountdownWindow(PopupEventType type, int[] openHours, int[] closeHours, int hour,
        int minute)
    {
        if (minute == 59 && Array.IndexOf(openHours, hour) >= 0)
        {
            state.SetEnabled(type, true);
            logger.LogInformation("Popup window opened: {Type} ({Hour}:{Minute} UTC)", type, hour, minute);
            return;
        }

        foreach (var (countdownMinute, remaining) in CountdownSchedule)
            if (minute == countdownMinute && Array.IndexOf(openHours, hour) >= 0)
            {
                logger.LogInformation(
                    "Popup window countdown: {Type} remaining={Remaining} ({Hour}:{Minute} UTC) -- client " +
                    "notice NOT sent: no wire sort code for this notice was given by the A12 contract, see " +
                    "openQuestions", type, remaining, hour, minute);
                return;
            }

        // Top-of-hour convention for the close transition -- see class remarks, CLOSE-MINUTE INFERENCE.
        if (minute == 0 && Array.IndexOf(closeHours, hour) >= 0)
        {
            state.SetEnabled(type, false);
            logger.LogInformation("Popup window closed: {Type} ({Hour}:{Minute} UTC)", type, hour, minute);
        }
    }

    private void ProcessSimpleWindow(PopupEventType type, int[] openHours, int[] closeHours, int hour, int minute)
    {
        // No countdown given for Invasion by the contract; open/close both use the same top-of-hour
        // convention as ProcessCountdownWindow's own close transition.
        if (minute != 0)
            return;

        if (Array.IndexOf(openHours, hour) >= 0)
        {
            state.SetEnabled(type, true);
            logger.LogInformation("Popup window opened: {Type} ({Hour}:{Minute} UTC)", type, hour, minute);
        }
        else if (Array.IndexOf(closeHours, hour) >= 0)
        {
            state.SetEnabled(type, false);
            logger.LogInformation("Popup window closed: {Type} ({Hour}:{Minute} UTC)", type, hour, minute);
        }
    }
}
