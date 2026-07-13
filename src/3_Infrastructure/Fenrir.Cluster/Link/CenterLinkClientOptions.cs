namespace Fenrir.Cluster.Link;

/// <summary>
///     Configuration for the OUTBOUND server-to-server link a LoginServer/GameServer maintains to the
///     CenterServer (the client half of <c>CenterServerHost</c>). Declared here in Fenrir.Cluster so both
///     Login and Game can reuse it; Main populates it (typically from <c>Center__Endpoint</c> /
///     <c>Center__SharedSecret</c>, or by copying the equivalent fields off
///     <c>GameServerOptions</c>/<c>LoginServerOptions</c>) via <c>AddCenterLinkClient</c>.
/// </summary>
public sealed class CenterLinkClientOptions
{
    /// <summary>
    ///     Convenience for a host that binds straight from the <c>Center</c> config section
    ///     (<c>Center__Endpoint</c> / <c>Center__SharedSecret</c>). Optional — Main may instead set the fields
    ///     below directly from another options object.
    /// </summary>
    public const string SectionName = "Center";

    /// <summary>
    ///     Where the CenterServer listens, in <c>host:port</c> or <c>tcp://host:port</c> form (injected by the
    ///     AppHost as <c>Center__Endpoint</c>). Null/empty/unparseable => the uplink stays dormant and the host
    ///     serves its own clients normally with no Center link.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    ///     Shared HMAC secret used for the handshake; MUST match the CenterServer's own
    ///     <c>Center__SharedSecret</c>. Empty => authentication is disabled and the uplink stays dormant
    ///     (fail-closed, symmetric with the accept side).
    /// </summary>
    public string? SharedSecret { get; set; }

    /// <summary>Per-attempt TCP connect timeout, in milliseconds.</summary>
    public int ConnectTimeoutMilliseconds { get; set; } = 5_000;

    /// <summary>Backoff delay before the first reconnect attempt (and the floor it resets to after a good link).</summary>
    public int InitialReconnectDelayMilliseconds { get; set; } = 1_000;

    /// <summary>Ceiling the reconnect backoff grows toward while the Center stays unreachable.</summary>
    public int MaxReconnectDelayMilliseconds { get; set; } = 30_000;
}
