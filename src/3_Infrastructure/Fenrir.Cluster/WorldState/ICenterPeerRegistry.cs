namespace Fenrir.Cluster.WorldState;

/// <summary>
///     The live registry of connected CenterServer S2S peers (zone/login links), exposing an idle-sweep for the
///     liveness host. DECLARED here (minimal) and CONSUMED by <c>PeerLivenessHost</c>; the implementation is
///     owned by Main / the session-and-link unit that holds the <c>CenterLinkSession</c> set and its per-peer
///     last-activity timestamps (refreshed by the S2S keepalive on every received frame).
/// </summary>
/// <remarks>
///     Reimplemented from the CenterServer idle sweep (Server/ts25center/S07_MyGame01.cpp:205-216): every peer
///     whose real elapsed time since last activity exceeds a wall-clock threshold is hard-disconnected.
/// </remarks>
public interface ICenterPeerRegistry
{
    /// <summary>
    ///     Disconnects every peer idle for longer than <paramref name="idleThreshold" /> as of
    ///     <paramref name="now" />. Returns how many peers were dropped, for logging.
    /// </summary>
    int DisconnectIdlePeers(TimeSpan idleThreshold, DateTimeOffset now);
}
