namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Pure, edge-triggered wall-clock decision for the autonomous daily-reset broadcast (case 1600): fires
///     exactly once per real occurrence of 00:01 (server wall clock -- UTC, same convention
///     <see cref="Simulation.GameDate" /> already documents as "always UTC" for this codebase), regardless of
///     how often <see cref="TryConsumeDueFire" /> is polled.
/// </summary>
/// <remarks>
///     Réf. C++ (via the A12 scheduled-events behavior contract) : Server/ts25center/S07_MyGame01.cpp:199-238
///     -- legacy fires once when its own tick observes hour 0 / minute 1, guarded by a one-shot flag that has
///     not yet been set that day, and clears that flag again one minute later (hour 0 / minute 2) to re-arm
///     for the next day.
///     <para>
///         DELIBERATE SIMPLIFICATION, same observable behavior: rather than reproduce the two-flag
///         set-then-clear machinery literally, this class tracks only "the minute-of-day value as of the
///         previous call" and only evaluates a transition when that value actually changes -- since real time
///         only ever moves forward, this fires on the FIRST call that observes hour 0/minute 1 (identical to
///         the legacy's one-shot flag) and naturally re-arms the next day without needing a second flag at
///         hour 0/minute 2 at all (the minute-of-day value will differ from "1" long before hour 0/minute 1
///         recurs 24h later). See <see cref="Simulation.PopupEventScheduleTimer" /> for the same idiom applied
///         to a family of hour-gated transitions.
///     </para>
/// </remarks>
public sealed class DailyResetBroadcastScheduler
{
    private int _lastMinuteOfDay = -1;

    /// <summary>
    ///     Returns <see langword="true" /> at most once per real occurrence of 00:01 UTC. Safe to call as
    ///     often as the caller likes (e.g. every few seconds) -- repeated calls within the same minute return
    ///     <see langword="false" />.
    /// </summary>
    public bool TryConsumeDueFire(DateTime utcNow)
    {
        var minuteOfDay = utcNow.Hour * 60 + utcNow.Minute;
        if (minuteOfDay == _lastMinuteOfDay)
            return false;

        _lastMinuteOfDay = minuteOfDay;
        return utcNow is { Hour: 0, Minute: 1 };
    }
}
