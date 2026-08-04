using System.Diagnostics.Metrics;

namespace Fenrir.Network.Dispatch.Sessions;

internal static class NetworkSessionMetrics
{
    private const string MeterName = "Fenrir.Network.Transport";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> OutboundBytes = Meter.CreateCounter<long>(
        "fenrir.network.outbound.bytes", "bytes", "Bytes accepted by a session transport output.");

    public static readonly Histogram<long> OutboundQueueDepth = Meter.CreateHistogram<long>(
        "fenrir.network.outbound.queue.depth", "frames", "Observed depth of a session outbound queue.");

    public static readonly Histogram<double> OutboundQueueAgeMs = Meter.CreateHistogram<double>(
        "fenrir.network.outbound.queue.age", "ms", "Time an outbound frame spent waiting in a session queue.");

    public static readonly Counter<long> OutboundQueueRejections = Meter.CreateCounter<long>(
        "fenrir.network.outbound.queue.rejections", "frames", "Outbound frames rejected by a session queue limit.");

    public static readonly Counter<long> SessionTerminations = Meter.CreateCounter<long>(
        "fenrir.network.sessions.terminated", "sessions", "Terminal session outcomes grouped by bounded disconnect reason.");

    private static readonly KeyValuePair<string, object?> LoginServerTag = new("server", "login");

    private static readonly KeyValuePair<string, object?> ZoneServerTag = new("server", "zone");

    public static KeyValuePair<string, object?> ServerTag(FenrirServer server)
    {
        return server == FenrirServer.Login ? LoginServerTag : ZoneServerTag;
    }

    public static KeyValuePair<string, object?> ReasonTag(DisconnectReason reason)
    {
        return new KeyValuePair<string, object?>("reason", reason.ToString());
    }
}
