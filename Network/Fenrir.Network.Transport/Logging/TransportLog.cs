using System.Net;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Transport.Logging;

internal static partial class TransportLog
{

        [LoggerMessage(
        EventId = 4201,
        EventName = "AcceptPortScanSwallowed",
        Level = LogLevel.Debug,
        Message =
            "Accept on {LocalEndPoint} failed with a transient socket error (half-open scan or similar) -- continuing the accept loop")]
    public static partial void AcceptPortScanSwallowed(this ILogger logger, Exception exception,
        EndPoint? localEndPoint);

        [LoggerMessage(
        EventId = 4202,
        EventName = "ConnectionConstructionFailed",
        Level = LogLevel.Warning,
        Message =
            "Failed to construct a session for an accepted connection on {LocalEndPoint} -- the accepted socket is being disposed without ever running")]
    public static partial void ConnectionConstructionFailed(this ILogger logger, Exception exception,
        EndPoint? localEndPoint);

        [LoggerMessage(
        EventId = 4203,
        EventName = "SendLoopFaulted",
        Level = LogLevel.Warning,
        Message =
            "Send loop faulted for connection {RemoteEndPoint} -- this connection can no longer send anything until an independent teardown path notices")]
    public static partial void SendLoopFaulted(this ILogger logger, Exception exception, EndPoint? remoteEndPoint);
}
