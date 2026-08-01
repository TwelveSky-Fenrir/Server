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
}
