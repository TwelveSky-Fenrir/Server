using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.FloodProtection;

internal static partial class FloodProtectionLog
{
    [LoggerMessage(
        EventId = 4101,
        EventName = "IpBlocked",
        Level = LogLevel.Warning,
        Message =
            "IP {IpAddress} blocked by the flood guard -- {KickedSessionCount} local session(s) aborted with DisconnectReason.IpBlocked")]
    public static partial void IpBlocked(this ILogger logger, string ipAddress, int kickedSessionCount);

    [LoggerMessage(
        EventId = 4102,
        EventName = "IpBlockPersistFailed",
        Level = LogLevel.Error,
        Message =
            "Failed to persist IP block for {IpAddress} -- in-memory counters still gate this IP for the lifetime of this process, but the block will not survive a restart")]
    public static partial void IpBlockPersistFailed(this ILogger logger, Exception exception, string ipAddress);

    [LoggerMessage(
        EventId = 4103,
        EventName = "IpBlockSkippedForOperatorAllowlist",
        Level = LogLevel.Warning,
        Message =
            "Flood-guard escalation for {IpAddress} suppressed: the IP is on admin.GmAllowlists, so no persistent block row was written and no session was kicked -- the in-memory per-IP connection cap still applies")]
    public static partial void IpBlockSkippedForOperatorAllowlist(this ILogger logger, string ipAddress);

    [LoggerMessage(
        EventId = 4104,
        EventName = "OperatorAllowlistLookupFailed",
        Level = LogLevel.Error,
        Message =
            "Operator-allowlist lookup failed for {IpAddress} -- treating the IP as NOT allowlisted so the flood guard stays effective; an operator IP may be blocked while this lookup is failing")]
    public static partial void OperatorAllowlistLookupFailed(this ILogger logger, Exception exception,
        string ipAddress);
}
