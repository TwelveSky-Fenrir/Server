namespace Fenrir.Application.Game.World;

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
    ///     Loaded once at world entry -- a hidden flag, never re-queried per chat message. A mute lifted or
    ///     newly applied mid-session is only picked up on the player's next world entry.
    /// </summary>
    public bool IsMuted { get; set; }
}
