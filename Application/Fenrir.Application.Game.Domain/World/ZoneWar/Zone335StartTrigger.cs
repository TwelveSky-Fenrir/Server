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
///     consumed only by the FFA-335 autonomous tick state machine (legacy <c>Process_Zone_335_FFA</c>,
///     <c>S07_MyGame01.cpp:10736-10850</c>), itself only ever invoked on the single zone-process instance whose
///     configured map equals <c>FFAMAPNUM</c> (335). That consuming tick is NOT modeled by this class or by
///     <c>GmFfaEventStartService</c> -- see that service's own remarks and the source behavior contract's own
///     "known functional overlap"/cross-process topology flag for why: this class only reproduces the two
///     process-local WRITES the GM command itself performs, matching this command's own source contract scope.
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
}
