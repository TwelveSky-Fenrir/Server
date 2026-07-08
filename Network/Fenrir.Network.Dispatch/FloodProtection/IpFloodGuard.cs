using System.Collections.Concurrent;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Network.Dispatch.FloodProtection;

/// <summary>
///     Unifies Fenrir's two independent legacy connection/error-flood triggers
///     (<c>Server/Header/enter_count.cpp</c>) behind one shared per-IP counter/registry, per this feature's own
///     requesting contract. Legacy itself keeps two separate, process-local <c>std::map</c>s with no shared
///     mechanism between them and no cross-process sharing at all; one <see cref="IpFloodGuard" /> instance is
///     likewise process-scoped (one per LoginServer, one per each GameServer shard) -- an intentional non-goal
///     per that contract's Edge Cases, not an oversight, since there is no legacy precedent to derive a
///     cross-process design from.
/// </summary>
/// <remarks>
///     <para>
///         <b>Trigger A</b> (raw-connection flood -- live in legacy production under the shipped
///         <c>LNW33</c>/<c>ReleaseEU33</c> build): <see cref="TryAcquireConnectionAsync" />. Boundary is strict
///         greater-than, matching <c>connect_count &gt; MAX_CONNECTIONS_PER_IP</c>
///         (<c>enter_count.cpp:5</c>, <c>Server/ts25login/S03_MyUser.cpp:195</c>) -- the block trips on the
///         (threshold+1)th concurrent connection, e.g. the 41st when the threshold is 40.
///     </para>
///     <para>
///         <b>Trigger B</b> (protocol-violation flood): <see cref="RecordProtocolViolationAsync" />. Boundary is
///         greater-than-or-equal, matching <c>error_count &gt;= MAX_ERROR_PACKET_PER_IP</c>
///         (<c>enter_count.cpp:6</c>) -- trips exactly on the 30th tallied violation when the threshold is 30.
///         Unlike Trigger A, legacy's own equivalent escalation function (<c>MyUserPacketError</c>) has had its
///         only two call sites commented out since before the current unknown-opcode handling was written
///         (<c>Server/ts25login/S04_MyWork01.cpp:39-65</c>, <c>Server/ts25zone/S04_MyWork01.cpp:274-301</c>) --
///         production legacy today disconnects on the very first unrecognized opcode with no per-IP counting at
///         all. A live, escalating threshold here is therefore NEW behavior beyond legacy parity: a deliberate,
///         flagged improvement, not a reproduction of dead code. Its hour window is also a clean per-IP
///         tumbling window, deliberately NOT reproducing legacy's own two bugs in that same dead function: a
///         single process-wide "last hour" variable (crossing an hour boundary free-passes one violation
///         server-wide and resets every OTHER IP's tally too, not just the current one).
///     </para>
///     <para>
///         Both triggers converge on the same <see cref="BlockAndKickAsync" />: the persisted block row can
///         never distinguish which trigger caused it, exactly as legacy leaves it (contract Side effects §1).
///         Unlike legacy Trigger A's kick loop -- whose bare, self-referential <c>Quit()</c>
///         (<c>Server/Header/Protocol/DEFINE.h:651</c>) only ever disconnects the single newly-connecting
///         session despite looping over every session, because <c>Exit()</c>'s connected-state guard makes every
///         other iteration a no-op (contract Side effects §2) -- this deliberately fixes that bug for both
///         triggers: every session sharing the offending IP, including whichever one just tripped the
///         threshold, is aborted.
///     </para>
/// </remarks>
public sealed class IpFloodGuard(
    int maxConnectionsPerIp,
    int maxProtocolViolationsPerIpPerHour,
    Func<string, CancellationToken, ValueTask> blockIpAsync,
    SessionRegistry sessionRegistry,
    Func<DateTime>? utcNowProvider = null)
{
    private readonly ConcurrentDictionary<string, int> _connectionCounts = new();
    private readonly Func<DateTime> _utcNowProvider = utcNowProvider ?? DefaultUtcNowProvider;
    private readonly ConcurrentDictionary<string, ViolationWindow> _violationWindows = new();

    private static DateTime DefaultUtcNowProvider()
    {
        return DateTime.UtcNow;
    }

    /// <summary>
    ///     Must be called exactly once per accepted connection, before any connect-acknowledgement packet is
    ///     sent (contract Preconditions/Outputs) -- mirrors legacy's own <c>InsertEnterCount</c> then
    ///     <c>GetEnterCount</c> ordering (<c>enter_count.cpp:15-26,47-57</c>). The caller must call
    ///     <see cref="ReleaseConnection" /> exactly once when the connection's socket eventually closes,
    ///     regardless of this method's return value.
    /// </summary>
    /// <returns>
    ///     <c>false</c> when this connection pushed <paramref name="ipAddress" />'s live concurrent-connection
    ///     gauge strictly over the configured threshold -- the IP has now been persistently blocked and every
    ///     session sharing it (including this brand-new one, once the caller has registered it) has been
    ///     aborted with <see cref="DisconnectReason.IpBlocked" />; the caller must not proceed (no greeting, no
    ///     dispatch loop).
    /// </returns>
    public async ValueTask<bool> TryAcquireConnectionAsync(string ipAddress, CancellationToken ct)
    {
        var count = _connectionCounts.AddOrUpdate(ipAddress, 1, static (_, existing) => existing + 1);

        if (count <= maxConnectionsPerIp)
            return true;

        await BlockAndKickAsync(ipAddress, ct).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    ///     Must be called exactly once for every connection whose <see cref="TryAcquireConnectionAsync" /> call
    ///     returned, when its socket is torn down -- mirrors <c>DeleteEnterCount</c>
    ///     (<c>enter_count.cpp:33-45</c>). A release with no matching prior acquire (should never happen) is a
    ///     benign no-op; the gauge never goes negative.
    /// </summary>
    public void ReleaseConnection(string ipAddress)
    {
        _connectionCounts.AddOrUpdate(ipAddress, 0, static (_, existing) => Math.Max(0, existing - 1));

        // Benign race with a concurrent TryAcquireConnectionAsync for the same IP (same trade-off as
        // SessionRateLimiter.Remove): the entry may briefly resurrect right after this removes it at zero, but
        // is never left orphaned above zero.
        _connectionCounts.TryRemove(new KeyValuePair<string, int>(ipAddress, 0));
    }

    /// <summary>
    ///     Records one unknown-opcode protocol violation for <paramref name="ipAddress" /> within the current
    ///     wall-clock hour. Call once per <c>Fenrir.Network.Framing.ProtocolViolationException</c> raised by
    ///     <c>FrameReader</c> for an already-connected session (contract Trigger B).
    /// </summary>
    public async ValueTask RecordProtocolViolationAsync(string ipAddress, CancellationToken ct)
    {
        var currentHour = _utcNowProvider().Ticks / TimeSpan.TicksPerHour;

        var window = _violationWindows.AddOrUpdate(
            ipAddress,
            _ => new ViolationWindow(currentHour, 1),
            (_, existing) => existing.HourBucket == currentHour
                ? existing with { Count = existing.Count + 1 }
                : new ViolationWindow(currentHour, 1));

        if (window.Count < maxProtocolViolationsPerIpPerHour)
            return;

        await BlockAndKickAsync(ipAddress, ct).ConfigureAwait(false);
    }

    private async ValueTask BlockAndKickAsync(string ipAddress, CancellationToken ct)
    {
        try
        {
            await blockIpAsync(ipAddress, ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Persisting the block must never fault the accept/dispatch path that triggered it -- matches
            // legacy's own fire-and-forget SetSqlBlockIP (contract Error/failure semantics: no visible retry or
            // alerting in the cited range either). Network.Dispatch takes no logging-framework dependency by
            // design (consistent with the rest of this project, e.g. SessionRateLimiter), so this failure is
            // silently absorbed rather than logged -- the in-memory counters above still gate future traffic
            // from this IP even if this particular write failed.
        }

        foreach (var session in sessionRegistry.SnapshotByRemoteAddress(ipAddress))
            session.Abort(DisconnectReason.IpBlocked);
    }

    private readonly record struct ViolationWindow(long HourBucket, int Count);
}
