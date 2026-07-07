namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Wall-clock instant of this character's last accepted CZ_HEARTBEAT_SEND (op151) -- deliberately
    ///     wall-clock, not zone-clock, since <see cref="Handlers.ZoneReadyHandler" />/<see cref="Handlers.HeartbeatHandler" />
    ///     run on the session loop and cannot see Zone's private simulated clock (same posture as
    ///     <see cref="FishingCastAtUtc" />). Null means "no heartbeat seen yet," matching legacy's
    ///     <c>mLastSentHeartbeat != -1</c> guard -- a session's very first ZoneReady always finds this null.
    /// </summary>
    public DateTime? LastSentHeartbeat { get; set; }

    /// <summary>
    ///     Legacy <c>mPrevSent</c> -- the <c>LastSend</c> counter value from this character's last accepted
    ///     heartbeat, compared by <see cref="Handlers.HeartbeatHandler" /> against the next one to reject a
    ///     replayed frame. Null (not 0) means "never sent" -- unlike legacy's truthy-zero sentinel, a
    ///     legitimate first <c>LastSend</c> of 0 is not mistaken for "no heartbeat yet."
    /// </summary>
    public uint? PrevSentHeartbeat { get; set; }

    /// <summary>
    ///     Wall-clock instant <see cref="Handlers.ZoneReadyHandler" /> last completed this character's op13
    ///     handshake without disconnecting it. Legacy restamps <c>mConnectTime</c> on every
    ///     CLIENT_OK_FOR_ZONE_SEND; Fenrir's op13 is a one-shot Registering-to-InWorld handshake (ZoneReadyRequest's
    ///     AllowedStates), so this is set exactly once per session. Null until then.
    /// </summary>
    public DateTime? ConnectTime { get; set; }

    /// <summary>
    ///     Legacy <c>mAutoTimeHack</c> -- strikes against a client that declares itself auto-hunting
    ///     (ZoneReadyRequest.AutoState &gt; 0) while <see cref="AutoHuntEnabled" /> is false server-side.
    ///     <see cref="Handlers.ZoneReadyHandler" /> disconnects on the 3rd strike, matching legacy exactly.
    /// </summary>
    public int AutoTimeHack { get; set; }

    /// <summary>
    ///     Loaded once at world entry (a hidden flag, no per-message wire cost) and, while this character stays
    ///     online, kept fresh by <c>MuteRefreshPollHost</c>'s periodic batch re-check
    ///     (<c>GameServerOptions.MutePollIntervalSeconds</c>, default 15s) -- the ONLY other legal writer of
    ///     this field besides world entry itself, via <see cref="ZoneCommandKind.SetMuted" /> so the
    ///     single-writer-per-zone invariant every other <see cref="PlayerRuntimeState" /> mutation relies on
    ///     still holds. A GM mute applied or lifted mid-session takes effect within one poll interval, not
    ///     only on the player's next world entry -- a bounded-staleness approximation of legacy's genuinely
    ///     live per-message recheck (uppercom-playuser-extra-relay-opcodes finding; see
    ///     <c>MuteRefreshPollHost</c>'s own remarks for why a literal per-message requery was rejected).
    /// </summary>
    public bool IsMuted { get; set; }
}
