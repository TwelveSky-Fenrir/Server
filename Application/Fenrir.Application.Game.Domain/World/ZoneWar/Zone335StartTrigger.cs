namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Process-local (this GameServer shard's own hosted process, mirroring legacy's per-<c>ts25zone.exe</c>
///     <c>MyGame</c> singleton) bookkeeping for the Elevated-tier zone-wide FFA-start GM command (legacy
///     PROCESS_DATA_SEND tSort 333, <c>Server/ts25zone/S04_MyWork04.cpp:1097-1131</c>): the countdown/
///     remaining-time counter (<c>mZone335TypeRemainTime2</c>) and the start-trigger flag
///     (<c>mZone335StartCommandCheck</c>). Deliberately NOT the same object as
///     <see cref="ZoneCenterSiegeState" />: that class holds the SHARED, cluster-wide FFA phase
///     (<see cref="ZoneCenterSiegeState.Zone335" />, kept in sync across every shard via center-broadcast
///     ingestion) this command reads as its own idle-state precondition, whereas the two fields here are
///     process-local scratch a GM-FFASTART invocation writes UNCONDITIONALLY once that precondition passes --
///     consumed by the FFA-335 autonomous tick state machine (legacy <c>Process_Zone_335_FFA</c>,
///     <c>S07_MyGame01.cpp:10736-10850</c>, ported as <see cref="Zone335FfaEventCycleSystem" />), itself only
///     ever invoked on the single zone-process instance whose configured map equals <c>FFAMAPNUM</c> (335) --
///     this class only reproduces the two process-local WRITES the GM command itself performs, matching this
///     command's own source contract scope; see <see cref="Zone335FfaEventCycleSystem" />'s own remarks for the
///     consuming half (<see cref="ConsumeStartRequest" />).
/// </summary>
public sealed class Zone335StartTrigger
{
    private readonly Lock _lock = new();
    private int _remainingTicks;
    private bool _startRequested;

    /// <summary>Countdown ticks remaining toward the FFA event's own countdown-to-open phase.</summary>
    public int RemainingTicks
    {
        get
        {
            lock (_lock)
            {
                return _remainingTicks;
            }
        }
    }

    /// <summary>
    ///     Whether a start has been requested and not yet consumed by a (currently unmodeled) FFA-335 tick.
    /// </summary>
    public bool StartRequested
    {
        get
        {
            lock (_lock)
            {
                return _startRequested;
            }
        }
    }

    /// <summary>
    ///     Writes both process-local fields together, atomically -- <paramref name="countdownTicks" /> is
    ///     already fully resolved by the caller (zero/negative-duration-input-to-default, positive-minutes-to-
    ///     ticks conversion both happen in <c>GmFfaEventStartService</c>, matching where the source behavior
    ///     contract places that decision).
    /// </summary>
    public void Request(int countdownTicks)
    {
        lock (_lock)
        {
            _remainingTicks = countdownTicks;
            _startRequested = true;
        }
    }

    /// <summary>
    ///     Atomically reads and clears <see cref="StartRequested" /> -- the FFA-335 tick's own idle-phase check
    ///     (<c>Zone335FfaEventCycleSystem</c>) calls this at most once per armed request, treating a true result
    ///     as "skip the remaining 60-minute idle wait, proceed straight into the countdown right now."
    ///     Deliberately does NOT surface <see cref="RemainingTicks" /> to the caller: per the source behavior
    ///     contract (Trigger section), the GM command's duration-in-minutes parameter is unconditionally
    ///     discarded once battle actually starts (a fixed 15-minute/1800-tick timer always wins instead), so the
    ///     only observable effect of a consumed request is "start now" -- the specific countdown value this
    ///     class stored is never actually read by anything, matching legacy parity rather than a missing
    ///     feature.
    /// </summary>
    public bool ConsumeStartRequest()
    {
        lock (_lock)
        {
            if (!_startRequested)
                return false;

            _startRequested = false;
            _remainingTicks = 0;
            return true;
        }
    }
}
