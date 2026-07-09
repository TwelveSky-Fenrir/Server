using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Logging;

/// <summary>
///     Operational log entries for the one chokepoint both of
///     <see cref="Fenrir.Network.Dispatch.FloodProtection.IpFloodGuard" />
///     's triggers converge on -- <c>IpFloodGuard.BlockAndKickAsync</c> (see that class's own remarks for
///     Trigger A/raw-connection-flood vs Trigger B/protocol-violation-flood). Neither trigger's own call site
///     (<c>TryAcquireConnectionAsync</c>, <c>RecordProtocolViolationAsync</c>) logs anything itself; centralizing
///     here, the same way <c>ClientSession.LogSessionStateChanged</c> centralizes session-state-transition
///     logging rather than trusting every <c>Mark*</c> setter call site to remember to log, guarantees every
///     successful block -- regardless of which trigger tripped it -- is observed exactly once.
///     <para>
///         Both methods are <see cref="LogLevel.Warning" />/<see cref="LogLevel.Error" />, generated via
///         <c>[LoggerMessage]</c> rather than a hand-written call for the same reason
///         <see cref="PacketLog" /> is: <see cref="IpBlocked" /> in particular can repeat at a non-trivial rate
///         for the exact scenario this class exists to handle -- an attacker who keeps reconnecting (or keeps
///         sending unrecognized opcodes) from an IP that is already blocked re-triggers
///         <c>BlockAndKickAsync</c>, and therefore this log call, on every single such attempt, not just the
///         first -- so the near-zero-cost-when-disabled/non-boxing shape matters here too, not only on
///         <see cref="PacketLog" />'s per-packet path.
///     </para>
/// </summary>
internal static partial class FloodProtectionLog
{
    /// <summary>
    ///     Fires once per successful <c>BlockAndKickAsync</c> call -- i.e. every time an IP actually trips
    ///     either flood trigger, regardless of whether persisting the block (see
    ///     <see cref="IpBlockPersistFailed" />) succeeded or failed; the in-memory kick still happens either way.
    ///     This is deliberately the primary, always-present operational signal for "an IP just got blocked" --
    ///     unlike the per-connection-host "rejected by IP flood guard" warning
    ///     (<c>LoginConnectionHost</c>/<c>GameConnectionHost</c>'s own <c>OnAcceptedAsync</c>), which only ever
    ///     fires for Trigger A (a rejected new connection) and never for Trigger B (a protocol-violation trip
    ///     against an already-connected session, which has no connection-host call site to log from at all).
    ///     <paramref name="kickedSessionCount" /> is <c>sessionRegistry.SnapshotByRemoteAddress(ipAddress)</c>'s
    ///     own count, captured once and reused for both the log and the abort loop -- never recomputed twice.
    /// </summary>
    [LoggerMessage(
        EventId = 4101,
        EventName = "IpBlocked",
        Level = LogLevel.Warning,
        Message =
            "IP {IpAddress} blocked by the flood guard -- {KickedSessionCount} local session(s) aborted with DisconnectReason.IpBlocked")]
    public static partial void IpBlocked(this ILogger logger, string ipAddress, int kickedSessionCount);

    /// <summary>
    ///     Fires when <c>blockIpAsync</c> (the injected <c>IFirewallRuleRepository.BlockAsync</c> delegate)
    ///     throws while persisting a block that <see cref="IpBlocked" /> already logged as having happened --
    ///     matches legacy's own fire-and-forget <c>SetSqlBlockIP</c> posture of never retrying or alerting
    ///     beyond this (contract Error/failure semantics), but unlike legacy this is no longer silently
    ///     discarded: the in-memory kick still succeeded (this IP's live sessions are gone either way), only the
    ///     durable block row is missing, meaning this IP is free to reconnect and re-trip the same trigger again
    ///     after this process restarts -- worth Error, not Warning, precisely because that persistence gap is
    ///     otherwise invisible until it's already been exploited.
    /// </summary>
    [LoggerMessage(
        EventId = 4102,
        EventName = "IpBlockPersistFailed",
        Level = LogLevel.Error,
        Message =
            "Failed to persist IP block for {IpAddress} -- in-memory counters still gate this IP for the lifetime of this process, but the block will not survive a restart")]
    public static partial void IpBlockPersistFailed(this ILogger logger, Exception exception, string ipAddress);
}
