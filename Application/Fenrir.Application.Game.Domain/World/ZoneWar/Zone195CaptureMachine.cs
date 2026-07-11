namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The four-state Zone195 "Nok-San" solo-capture machine (Server/ts25zone/S07_MyGame01.cpp:8385-8602).
///     <see cref="Commit" /> is a transient transition, never observed between ticks: the reward grant, stone
///     flip, and state broadcast all happen inline in the single tick that reaches it, then the machine resets
///     straight back to <see cref="IdleSearching" />.
/// </summary>
public enum Zone195CapturePhase : byte
{
    /// <summary>No capture in progress -- scanning every tick for the first eligible challenger.</summary>
    IdleSearching = 0,

    /// <summary>
    ///     A challenger is locked; holding a ~6-second settle delay (one tenth of a game-minute) before the countdown
    ///     starts.
    /// </summary>
    Settle = 1,

    /// <summary>Counting down toward capture at ~1-game-minute intervals while re-validating the locked challenger every tick.</summary>
    Countdown = 2,

    /// <summary>
    ///     Transient -- the reward/flip/broadcast are applied inline, then the machine resets to
    ///     <see cref="IdleSearching" />.
    /// </summary>
    Commit = 3
}

/// <summary>
///     Per-map mutable state for one hosted Nok-San capture instance. A dumb state bag driven entirely by
///     <see cref="Fenrir.Application.Game.Domain.Simulation.Zone195NokSanSystem" /> -- it holds no logic of
///     its own. Only ever touched from its own map's zone tick thread (the system resolves one instance per
///     map from a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" /> and never
///     shares an instance across maps), the same single-writer-per-zone invariant every other per-map RvR
///     state registry in this cluster relies on (<see cref="ValleyWarKillRegistry" />).
/// </summary>
public sealed class Zone195CaptureMachine
{
    /// <summary>Sentinel for "no challenger currently locked".</summary>
    public const int NoCapturer = -1;

    public Zone195CapturePhase Phase { get; set; } = Zone195CapturePhase.IdleSearching;

    /// <summary>
    ///     The pinned challenger's <c>CharacterId</c> while a capture is in progress, else <see cref="NoCapturer" />.
    ///     Fenrir pins by <c>CharacterId</c> alone: unlike the legacy's (user-slot-index, unique-number) pair,
    ///     a Fenrir <c>CharacterId</c> is already globally unique and stable for the character's whole lifetime
    ///     in the zone, so the anti-swap invariant (a reused slot with a mismatched unique number is treated as
    ///     the capturer having left, Server/ts25zone/S07_MyGame01.cpp:8417-8418,8434,8464) is satisfied
    ///     inherently -- a different character is a different id, and the original leaving removes that id.
    /// </summary>
    public int CapturerCharacterId { get; set; } = NoCapturer;

    /// <summary>The locked challenger's tribe at lock time -- carried into the success broadcast/flip.</summary>
    public byte CapturerTribe { get; set; }

    /// <summary>The locked challenger's name at lock time -- carried into the appeared/success broadcasts.</summary>
    public string CapturerName { get; set; } = string.Empty;

    /// <summary>
    ///     Remaining-time counter, initialized to 5 at lock (Server/ts25zone/S07_MyGame01.cpp:8385-8420) and
    ///     decremented once per settle/countdown broadcast until it reaches zero at capture.
    /// </summary>
    public int RemainingTime { get; set; }

    /// <summary>
    ///     Legacy ticks accumulated in the current timed phase (<see cref="Zone195CapturePhase.Settle" />/
    ///     <see cref="Zone195CapturePhase.Countdown" />) toward the next threshold crossing. Burst-tolerant: a
    ///     stalled host advancing several ticks at once is paid in full here, matching every other burst-
    ///     tolerant countdown in this cluster.
    /// </summary>
    public int PhaseAccumulatorTicks { get; set; }

    /// <summary>Clears every capture-in-progress field back to the idle/searching baseline.</summary>
    public void ResetToIdle()
    {
        Phase = Zone195CapturePhase.IdleSearching;
        CapturerCharacterId = NoCapturer;
        CapturerTribe = 0;
        CapturerName = string.Empty;
        RemainingTime = 0;
        PhaseAccumulatorTicks = 0;
    }
}
