namespace Fenrir.Application.Login.Abstractions.ZoneTransfer;

/// <summary>
///     Confirms a candidate shard's Host:Port actually accepts a TCP connection right now, before
///     <see cref="IZoneTransferService" /> mints a handover ticket for it. The shard directory
///     (<c>runtime.GameServerDirectory</c>) only proves the shard heartbeated recently -- up to ~17s ago,
///     accounting for the SQL freshness window plus the repository's own read cache -- never that it is
///     still alive at the moment a ticket is about to be minted for it.
/// </summary>
public interface IShardReachabilityProbe
{
    /// <summary>
    ///     Attempts a short, bounded-timeout TCP connect to <paramref name="host" />:<paramref name="port" />.
    ///     Returns <c>true</c> only if the connection actually succeeds within the configured timeout. Any
    ///     failure to connect (refused, timed out, DNS failure) resolves to <c>false</c> rather than
    ///     throwing -- a probe result is advisory input to a routing decision, never itself a caller-visible
    ///     error. Cancelling <paramref name="ct" /> propagates as a genuine
    ///     <see cref="System.OperationCanceledException" />, distinct from a probe timeout.
    /// </summary>
    public ValueTask<bool> IsReachableAsync(string host, int port, CancellationToken ct);
}
